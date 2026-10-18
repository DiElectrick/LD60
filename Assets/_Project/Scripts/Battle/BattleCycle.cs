using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives one run: a <see cref="MapRoute"/> assigns the player's path.
/// <para>
/// Every stop of the route is one battle - the encounter of the stop's map node - after
/// which that node's station opens (module panel plus the shop / repair it lists). "Next"
/// then travels on to the next stop. After the last stop the boss encounter is fought and,
/// once it is dead, the run is over: no more battles, no station, just the end screen.
/// </para>
/// </summary>
public class BattleCycle : MonoBehaviour, IBootstraped
{
    /// <summary>Folder the default shop / repair buildings are loaded from.</summary>
    public const string StopBuildingFolder = "StopBuildings";

    public bool isInitialised { get; set; }

    [Tooltip("The player's path. Empty = the first MapRoute in Resources/" + MapRoute.ResourceFolder + ".")]
    [SerializeField] MapRoute route;

    [Header("Fallback (no route assigned)")]
    [Tooltip("Encounter fought when there is no route at all.")]
    public EncounterData startingEncounter;

    [Tooltip("Node whose station opens after every victory when there is no route.")]
    [SerializeField] MapNode startingNode;

    [Header("Victory animation")]
    [Tooltip("Minimum time the city keeps rolling at full speed after the last enemy dies, before " +
             "the brake starts. The building of the stop scrolls in during this time; when it needs " +
             "longer to reach the city, the coast stretches so it never pops in on screen.")]
    [Range(0f, 8f)]
    [SerializeField] float victoryCoastDuration = 4.5f;

    [Tooltip("How long the parallax takes to come to a full stop.")]
    [SerializeField] float stopDuration = 1.8f;

    [Header("Stop building")]
    [Tooltip("Building the city stops in front of when the stop offers a shop. " +
             "Empty = Resources/" + StopBuildingFolder + "/ShopBuilding.")]
    [SerializeField] StopBuilding shopBuilding;

    [Tooltip("Building the city stops in front of when the stop offers a repair bay. " +
             "Empty = Resources/" + StopBuildingFolder + "/RepairBuilding.")]
    [SerializeField] StopBuilding repairBuilding;

    [Tooltip("Free space kept between the city and the building it stops in front of.")]
    [SerializeField] float stopBuildingGap = 0.7f;

    [SerializeField] float moduleShakeAmplitude = 0.06f;
    [SerializeField] float platformShakeAmplitude = 0.08f;

    [Header("Run end")]
    [Tooltip("Travel transition that carries the city between the stops (and plays the end " +
             "screen after the boss). Empty = found in the scene.")]
    [SerializeField] TravelTransition travel;

    TurnManager turnManager;
    EnemyManager enemyManager;
    EnemySpawner spawner;
    ModulePanelUI modulePanel;
    StationController station;
    ParallaxSystem parallax;
    CityController city;

    EncounterData currentEncounter;
    bool isBattleRunning;
    Tween spawnDelay;

    // Building the city stops in front of at the current stop (null while fighting / travelling).
    StopBuilding currentBuilding;
    GroundParallaxLayer groundLayer;

    // Victory sequence: coast (world keeps rolling) -> brake + shake -> station / end screen.
    Tween coastDelay;
    Tween panelDelay;

    // Where the run currently is: the stop being travelled to, the boss, or the end.
    int stopIndex;
    bool isBossBattle;
    bool isRunOver;

    /// <summary>True while the station slides away and the next stop has not started yet.</summary>
    bool isAdvancing;

    static MapNode defaultNode;

    /// <summary>Raised when the boss dies and the run is over (hook the end screen here).</summary>
    public event Action RunCompleted;

    /// <summary>The path of this run (assigned in the inspector, or loaded from Resources).</summary>
    public MapRoute Route => route;

    /// <summary>Index of the stop the city is travelling to / standing at (0-based).</summary>
    public int StopIndex => stopIndex;

    /// <summary>Number of stops of the route (0 when there is no route).</summary>
    public int StopCount => route != null ? route.StopCount : 0;

    /// <summary>True while the boss encounter is running.</summary>
    public bool IsBossBattle => isBossBattle;

    /// <summary>True once the boss is dead - nothing else is fought.</summary>
    public bool IsRunOver => isRunOver;

    /// <summary>Node of the stop being travelled to / stood at. Null during the boss fight.</summary>
    public MapNode CurrentNode
    {
        get
        {
            if (isBossBattle) return null;

            if (route != null) return route.GetStop(stopIndex);

            return startingNode != null ? startingNode : DefaultNode();
        }
    }

    /// <summary>Encounter of the current step: the stop's own encounter, or the boss.</summary>
    public EncounterData CurrentEncounter
    {
        get
        {
            if (isBossBattle) return route != null ? route.bossEncounter : null;

            var node = CurrentNode;
            return node != null && node.encounter != null ? node.encounter : startingEncounter;
        }
    }

    public void Init()
    {
        turnManager = G.Get<TurnManager>();
        enemyManager = G.Get<EnemyManager>();
        spawner = G.Get<EnemySpawner>();
        city = G.Get<CityController>();
        station = G.Get<StationController>();
        parallax = FindAnyObjectByType<ParallaxSystem>();

        if (travel == null)
            travel = FindAnyObjectByType<TravelTransition>(FindObjectsInactive.Include);

        modulePanel = FindAnyObjectByType<ModulePanelUI>(FindObjectsInactive.Include);
        if (modulePanel != null)
        {
            modulePanel.OnNext += OnNextPressed;
            modulePanel.Hide();
        }
        else
        {
            Debug.LogWarning("[Battle] No ModulePanelUI in the scene");
        }
    }

    void Start()
    {
        BeginRun();
    }

    // ------------------------------------------------------------------ run / route

    /// <summary>Starts a fresh run at the first stop of the route.</summary>
    public void BeginRun()
    {
        stopIndex = 0;
        isBossBattle = false;
        isRunOver = false;
        isAdvancing = false;

        if (route == null) route = MapRoute.LoadDefault();

        if (route == null || !route.HasStops)
        {
            Debug.LogWarning($"[Battle] No map route in Resources/{MapRoute.ResourceFolder} - " +
                             "falling back to the starting node / encounter");

            StartEncounter(CurrentEncounter);
            return;
        }

        Debug.Log($"[Battle] Run '{route.displayName}': {StopCount} stops " +
                  $"({route.CountStopsWith("shop")} shop, {route.CountStopsWith("repair")} repair), " +
                  $"boss '{NameOf(route.bossEncounter)}'");
        Debug.Log($"[Battle] Path: {DescribeRoute(route)}");

        StartStopBattle();
    }

    /// <summary>Jumps to a stop of the route (debug / testing helper).</summary>
    public void EnterStop(int index)
    {
        if (route == null || !route.HasStops) return;

        stopIndex = Mathf.Clamp(index, 0, route.StopCount - 1);
        isBossBattle = false;
        isRunOver = false;
        isAdvancing = false;

        station?.CloseAll();
        StartStopBattle();
    }

    /// <summary>Fights the battle that leads to the stop the run is currently at.</summary>
    void StartStopBattle()
    {
        var node = CurrentNode;

        if (node == null)
        {
            Debug.LogWarning($"[Battle] Route stop {stopIndex + 1}/{StopCount} is empty - ending the run");
            EndRun();
            return;
        }

        Debug.Log($"[Battle] Stop {stopIndex + 1}/{StopCount}: {node.displayName} " +
                  $"[{string.Join("+", node.stationPanels)}]");

        StartEncounter(CurrentEncounter);
    }

    /// <summary>Travels on after a station: the next stop, or the boss after the last one.</summary>
    void AdvanceAfterStop()
    {
        if (route == null || !route.HasStops)
        {
            // No route: the fallback simply repeats its encounter.
            StartEncounter(CurrentEncounter);
            return;
        }

        stopIndex++;

        if (stopIndex < route.StopCount) StartStopBattle();
        else StartBossBattle();
    }

    /// <summary>The boss encounter after the last stop. A normal encounter - it just ends the run.</summary>
    void StartBossBattle()
    {
        if (route.bossEncounter == null)
        {
            Debug.LogWarning("[Battle] The route has no boss encounter - the run ends after the last stop");
            EndRun();
            return;
        }

        isBossBattle = true;

        Debug.Log($"[Battle] Boss: '{route.bossEncounter.displayName}'");
        StartEncounter(route.bossEncounter);
    }

    /// <summary>Run over: everything stops, no station opens and nothing else is spawned.</summary>
    void EndRun()
    {
        isRunOver = true;
        isBossBattle = false;
        isBattleRunning = false;

        station?.CloseAll();
        turnManager?.StopBattle();
        spawner?.DespawnAll();
        ClearVictorySequence();
        parallax?.BrakeToStop(stopDuration);

        Debug.Log("[Battle] Run complete - every stop cleared and the boss defeated");

        // The last travel keeps its screen: full bar, message and restart button.
        if (travel != null) travel.PlayEnd(NodeProgress(StopCount), RestartRun);
        else Debug.LogWarning("[Battle] No travel transition in the scene - no end screen");

        RunCompleted?.Invoke();
    }

    /// <summary>Starts a new run: the scene is reloaded, so city, stations and route start fresh.</summary>
    void RestartRun()
    {
        spawnDelay?.Kill();
        ClearVictorySequence();

        // Services of the old scene must not survive into the new one.
        G.Clear();

        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    static string DescribeRoute(MapRoute route)
    {
        var parts = new List<string>();

        for (int i = 0; i < route.StopCount; i++)
        {
            var node = route.GetStop(i);
            string name = node != null ? node.displayName : "missing";
            string panels = node != null ? string.Join("+", node.stationPanels) : "?";

            parts.Add($"{i + 1}. {name} [{panels}]");
        }

        parts.Add($"boss: {NameOf(route.bossEncounter)}");

        return string.Join(" -> ", parts);
    }

    static string NameOf(EncounterData encounter)
    {
        return encounter != null ? encounter.displayName : "none";
    }

    /// <summary>Node used when there is no route and no starting node: first MapNode in Resources.</summary>
    static MapNode DefaultNode()
    {
        if (defaultNode == null)
        {
            var nodes = Resources.LoadAll<MapNode>(MapNode.ResourceFolder);
            if (nodes.Length > 0) defaultNode = nodes[0];
        }

        return defaultNode;
    }

    // ------------------------------------------------------------------ battles

    public void StartEncounter(EncounterData encounter)
    {
        if (encounter == null || encounter.enemies.Count == 0)
        {
            Debug.LogWarning("[Battle] No encounter to start");
            return;
        }

        currentEncounter = encounter;

        // Never keep ticking an old battle whose enemies are about to be despawned.
        // A pending victory sequence is dropped too: the world must not brake or pop
        // the panel open in the middle of the next fight.
        turnManager?.StopBattle();
        ClearVictorySequence();
        isBattleRunning = false;

        // 1. spawn the enemies described by the data
        var spawnedEnemies = spawner.SpawnEncounter(encounter.enemies);

        // 2. the battle only starts once every enemy has flown into its slot
        parallax?.ResumeScrolling();

        spawnDelay?.Kill();
        spawnDelay = DOVirtual.DelayedCall(spawner.FlyInDuration, () => BeginBattle(spawnedEnemies, encounter));
    }

    void BeginBattle(List<BaseEnemy> spawnedEnemies, EncounterData encounter)
    {
        // Drop anything that was destroyed while the enemies were flying in.
        var turners = new List<ITurner>();
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null) turners.Add(enemy);
        }

        if (turners.Count == 0) return;

        turnManager.InitBattle(turners);
        isBattleRunning = true;

        Debug.Log($"[Battle] Started '{encounter.displayName}' with {turners.Count} enemies");
    }

    void Update()
    {
        if (!isBattleRunning) return;
        if (enemyManager.Enemies.Count > 0) return;

        EndBattle();
    }

    void EndBattle()
    {
        isBattleRunning = false;
        turnManager.StopBattle();

        // Dying enemies already detached themselves from the spawner, so this only
        // clears leftovers - the falling corpses keep their death animation.
        spawner.DespawnAll();

        ClearVictorySequence();

        // The victory is not an instant stop: the city coasts on at full speed for a
        // moment (corpses fall, the world keeps scrolling), and only then the brake
        // kicks in. ParallaxSystem is left untouched during the coast on purpose -
        // its speedMultiplier is exactly what "the city is moving" means.
        float coast = Mathf.Max(0f, victoryCoastDuration);

        // The building of the stop scrolls in with the ground and the brake is timed so the
        // city comes to a halt right in front of it.
        var building = SpawnStopBuilding();
        if (building != null) coast = PlaceStopBuilding(building, coast);

        Debug.Log(building != null
            ? $"[Battle] Victory - coasting for {coast:F2}s while '{building.name}' rolls in"
            : $"[Battle] Victory - coasting for {coast:F2}s before the stop animation");

        if (coast <= 0f) PlayStopAnimation();
        else coastDelay = DOVirtual.DelayedCall(coast, PlayStopAnimation);

        // The station (or the end screen after the boss) slides in once everything is still.
        panelDelay = DOVirtual.DelayedCall(coast + stopDuration, AfterVictory);
    }

    // ------------------------------------------------------------------ stop building

    /// <summary>
    /// Shop / repair building of the stop the city is standing at: null for a node without one
    /// (and for the boss, which has no station at all).
    /// </summary>
    StopBuilding StopBuildingPrefab()
    {
        var node = CurrentNode;
        if (node == null) return null;

        if (node.stationPanels.Contains("repair"))
            return repairBuilding != null ? repairBuilding : LoadStopBuilding("RepairBuilding");

        if (node.stationPanels.Contains("shop"))
            return shopBuilding != null ? shopBuilding : LoadStopBuilding("ShopBuilding");

        return null;
    }

    static StopBuilding LoadStopBuilding(string fileName)
    {
        return Resources.Load<StopBuilding>($"{StopBuildingFolder}/{fileName}");
    }

    /// <summary>Spawns the stop's building off the right edge of the road, ready to roll in.</summary>
    StopBuilding SpawnStopBuilding()
    {
        var prefab = StopBuildingPrefab();
        if (prefab == null) return null;

        var building = Instantiate(prefab);
        building.name = prefab.name;

        // It has to travel exactly like the road, or it would slide out from under the city.
        var ground = GroundLayer();
        if (ground != null) building.speed = ground.speed;

        currentBuilding = building;
        return building;
    }

    /// <summary>
    /// Places the building so that it reaches the city exactly when the world stops, and returns
    /// the coast time the victory sequence must use (never shorter than
    /// <see cref="victoryCoastDuration"/>, never short enough to appear on screen).
    /// </summary>
    float PlaceStopBuilding(StopBuilding building, float minimumCoast)
    {
        float speed = Mathf.Max(0.01f, WorldSpeed);
        float brakeDistance = BrakeDistance;

        // Where the building must end: right in front of the city, its left edge one gap away.
        float stopX = CityRightEdge() + stopBuildingGap + building.HalfWidth;

        // It must be spawned outside the view, so give the coast at least the time it needs to
        // come in from the right edge.
        float entry = (building.ViewRightEdge + building.HalfWidth - stopX - brakeDistance) / speed;
        float coast = Mathf.Max(Mathf.Max(0f, minimumCoast), entry);

        // Coast at full speed for `coast`, then slide the last bit while braking: that is how far
        // to the right the building has to start.
        var start = building.transform.position;
        building.transform.position =
            new Vector3(stopX + coast * speed + brakeDistance, start.y, start.z);

        return coast;
    }

    /// <summary>Effective road speed right now (ground layer speed × the live parallax multiplier).</summary>
    float WorldSpeed
    {
        get
        {
            var ground = GroundLayer();
            float layerSpeed = ground != null ? ground.speed : 1f;
            float multiplier = parallax != null ? parallax.speedMultiplier : 1f;

            return layerSpeed * multiplier;
        }
    }

    /// <summary>
    /// Distance the world still slides while <see cref="ParallaxSystem.BrakeToStop"/> runs: the
    /// brake eases out (OutQuad), so it travels a third of the stopping time at full speed.
    /// </summary>
    float BrakeDistance => WorldSpeed * Mathf.Max(0f, stopDuration) / 3f;

    GroundParallaxLayer GroundLayer()
    {
        if (groundLayer != null || parallax == null) return groundLayer;

        foreach (var layer in parallax.Layers)
        {
            if (layer is GroundParallaxLayer ground)
            {
                groundLayer = ground;
                break;
            }
        }

        return groundLayer;
    }

    /// <summary>Right edge of the city: the part of the caravan the building stops in front of.</summary>
    float CityRightEdge()
    {
        if (city == null) return 0f;

        float right = city.transform.position.x;

        foreach (var platform in city.Platforms)
        {
            if (platform == null) continue;

            var art = platform.GetComponent<Renderer>();
            right = art != null
                ? Mathf.Max(right, art.bounds.max.x)
                : Mathf.Max(right, platform.transform.position.x);
        }

        return right;
    }

    /// <summary>Removes the building of the previous stop (the city rolled on).</summary>
    void ClearStopBuilding()
    {
        if (currentBuilding == null) return;

        Destroy(currentBuilding.gameObject);
        currentBuilding = null;
    }

    /// <summary>Brake the world and make the city shiver while it comes to a halt.</summary>
    void PlayStopAnimation()
    {
        coastDelay = null;

        Debug.Log("[Battle] Braking the world to a stop");

        parallax?.BrakeToStop(stopDuration);
        ShakeCity();
    }

    /// <summary>Drops any pending victory animation (used when a new battle starts).</summary>
    void ClearVictorySequence()
    {
        coastDelay?.Kill();
        coastDelay = null;

        panelDelay?.Kill();
        panelDelay = null;

        // The next stop gets its own building; this one is left behind.
        ClearStopBuilding();
    }

    void ShakeCity()
    {
        if (city == null) return;

        foreach (var platform in city.Platforms)
        {
            platform.GetComponent<Shaker>()?.Shake(platformShakeAmplitude, stopDuration);

            foreach (var module in platform.Modules)
            {
                module.GetComponent<Shaker>()?.Shake(moduleShakeAmplitude, stopDuration);
            }
        }
    }

    /// <summary>
    /// After a victory: opens the station of the current stop (module panel plus whatever
    /// else the node lists), or ends the run when the fallen enemy was the boss.
    /// </summary>
    void AfterVictory()
    {
        panelDelay = null;
        isAdvancing = false;

        // The boss has no station - beating it is the end of the run.
        if (isBossBattle)
        {
            EndRun();
            return;
        }

        if (station != null && CurrentNode != null)
        {
            Debug.Log($"[Battle] Station of '{CurrentNode.displayName}': " +
                      $"{string.Join(", ", CurrentNode.stationPanels)}");
            station.Open(CurrentNode);
            return;
        }

        if (modulePanel != null)
        {
            modulePanel.Open(0);
            return;
        }

        StartEncounter(CurrentEncounter);
    }

    void OnNextPressed()
    {
        if (isRunOver || isAdvancing) return;

        // A second click while the station slides away must not skip a stop.
        isAdvancing = true;

        // Closing the station first, so the shop slides away with the module panel.
        station?.CloseAll();

        // Travel first: the city is not thrown into the next fight, it rolls on behind a
        // foreground object while the route bar fills half-way to the next node - the battle
        // is fought on the way there, before the city actually reaches the node.
        if (travel != null && travel.CanPlay)
        {
            // The world speeds back up; the transition itself waits for the city to get going.
            parallax?.ResumeScrolling(travel.RampDuration);

            travel.Play(NodeProgress(stopIndex), NodeProgress(stopIndex + 0.5f), AdvanceAfterStop);
            return;
        }

        AdvanceAfterStop();
    }

    /// <summary>
    /// Position on the travel bar, 0..1, in node units: 0 = the first stop, StopCount = the
    /// boss. A stop is reached after its battle, so a travel only covers half the gap to the
    /// next node - the rest of the way is the fight itself.
    /// </summary>
    float NodeProgress(float nodeIndex)
    {
        int nodes = StopCount + 1;
        if (nodes <= 0) return 0f;

        return Mathf.Clamp01((nodeIndex + 1f) / nodes);
    }

    void OnDestroy()
    {
        // Unlinked DOTween delays outlive the object, so drop them explicitly.
        spawnDelay?.Kill();
        ClearVictorySequence();
    }
}
