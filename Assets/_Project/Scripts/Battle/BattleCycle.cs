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
    [Tooltip("How long the city drives at full speed after the route screen has slid away, before " +
             "the brake starts. The stop building scrolls in during this time; when it needs " +
             "longer to reach the city, the drive stretches so it never pops in on screen.")]
    [Range(0f, 8f)]
    [SerializeField] float arrivalDriveDuration = 2.2f;

    [Tooltip("How long the parallax takes to come to a full stop.")]
    [SerializeField] float stopDuration = 1.8f;

    [Header("Between the stops")]
    [Tooltip("How long the city drives out of a station before the next enemy shows up.")]
    [Range(0f, 10f)]
    [SerializeField] float driveBeforeBattle = 3.5f;

    [Tooltip("Beat after the station closes before the city pulls away: the panels slide off " +
             "screen first, then the engine spins up and the world starts to roll.")]
    [Range(0f, 2f)]
    [SerializeField] float departDelay = 0.5f;

    [Header("Stop building")]
    [Tooltip("Building the city stops in front of when the stop offers a shop. " +
             "Empty = Resources/" + StopBuildingFolder + "/ShopBuilding.")]
    [SerializeField] StopBuilding shopBuilding;

    [Tooltip("Building the city stops in front of when the stop offers a repair bay. " +
             "Empty = Resources/" + StopBuildingFolder + "/RepairBuilding.")]
    [SerializeField] StopBuilding repairBuilding;

    [Tooltip("Free space kept between the city and the building it stops in front of.")]
    [SerializeField] float stopBuildingGap = 0.7f;

    [Header("City destroyed")]
    [Tooltip("Pause after the city dies before the first module blows up.")]
    [SerializeField] float deathDelay = 0.7f;

    [Tooltip("Time between two module explosions.")]
    [SerializeField] float moduleExplosionGap = 0.35f;

    [Tooltip("Pause after the last module, before the platforms blow up.")]
    [SerializeField] float platformExplosionDelay = 0.5f;

    [Tooltip("Time between two platform explosions.")]
    [SerializeField] float platformExplosionGap = 0.45f;

    [Tooltip("Pause after the platforms are gone before the end screen slides in.")]
    [SerializeField] float endScreenDelay = 0.9f;

    [Header("Start camp")]
    [Tooltip("How long the world takes to come to a stop when the run starts at a camp.")]
    [SerializeField] float campBrakeDuration = 0.6f;

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
    float stopBuildingX;
    GroundParallaxLayer groundLayer;

    // Victory sequence: coast (world keeps rolling) -> brake + shake -> station / end screen.
    Tween coastDelay;
    Tween panelDelay;

    // Where the run currently is: the stop being travelled to, the boss, or the end.
    int stopIndex;
    bool isBossBattle;
    bool isRunOver;
    bool isDefeated;

    /// <summary>True while the station slides away and the next stop has not started yet.</summary>
    bool isAdvancing;

    static MapNode defaultNode;

    /// <summary>Raised when the boss dies and the run is over (hook the end screen here).</summary>
    public event Action RunCompleted;

    /// <summary>Raised when a battle actually starts - the enemies are in and the turn loop runs.</summary>
    public event Action BattleStarted;

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

    /// <summary>True once the city was destroyed - the run ended in defeat.</summary>
    public bool IsDefeated => isDefeated;

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

        // Health gone = the run is over, however far along the route the city was.
        if (city != null) city.Destroyed += OnCityDestroyed;

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

        // The run can start at a camp: no fight, the station (module panel) opens right away.
        if (CurrentNode != null && CurrentNode.isCamp)
        {
            BeginAtCamp(CurrentNode);
            return;
        }

        StartStopBattle();
    }

    /// <summary>Starts the run on a camp: the world is parked and the station opens without a battle.</summary>
    void BeginAtCamp(MapNode camp)
    {
        Debug.Log($"[Battle] Starting at camp '{camp.displayName}' [{string.Join("+", camp.stationPanels)}]");

        isBattleRunning = false;
        spawner?.DespawnAll();
        turnManager?.StopBattle();
        ClearVictorySequence();

        // The city stands still while the player prepares it.
        parallax?.BrakeToStop(campBrakeDuration);

        if (station != null) station.Open(camp);
        else modulePanel?.Open(0);
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

    /// <summary>Run over: no station opens and nothing else is spawned, but the city keeps driving.</summary>
    void EndRun()
    {
        isRunOver = true;
        isBossBattle = false;
        isBattleRunning = false;

        station?.CloseAll();
        turnManager?.StopBattle();
        spawner?.DespawnAll();
        ClearVictorySequence();

        // The boss was the last stop of the road, so the run ends on the move: the city rolls on
        // into the sunset while the end screen comes in over it. No brake, no parking.
        parallax?.ResumeScrolling();

        Debug.Log("[Battle] Run complete - every stop cleared and the boss defeated");

        // The last travel keeps its screen: full bar, message and restart button.
        ShowEndScreen(victory: true);

        RunCompleted?.Invoke();
    }

    // ------------------------------------------------------------------ city destroyed

    /// <summary>
    /// The city's health reached zero: the battle stops and the city falls apart module by module,
    /// then the platforms, and finally the end screen rolls in like the travel between stops.
    /// </summary>
    void OnCityDestroyed()
    {
        if (isDefeated || isRunOver) return;

        isDefeated = true;
        isBattleRunning = false;

        turnManager?.StopBattle();
        station?.CloseAll();
        modulePanel?.Hide();
        ClearVictorySequence();

        Debug.Log("[Battle] Defeat - the city is destroyed, the run is over");

        StopAllCoroutines();
        StartCoroutine(DeathRoutine());
    }

    System.Collections.IEnumerator DeathRoutine()
    {
        var fx = G.Get<ExplosionFX>();

        if (deathDelay > 0f) yield return new WaitForSeconds(deathDelay);

        // 1. every module blows up, one after another
        var modules = CollectModules();
        foreach (var module in modules)
        {
            if (module == null) continue;

            Explode(fx, module.transform.position);
            Destroy(module.gameObject);

            if (moduleExplosionGap > 0f) yield return new WaitForSeconds(moduleExplosionGap);
        }

        if (platformExplosionDelay > 0f) yield return new WaitForSeconds(platformExplosionDelay);

        // 2. then the platforms the city was standing on
        if (city != null)
        {
            foreach (var platform in new List<Platform>(city.Platforms))
            {
                if (platform == null) continue;

                Explode(fx, platform.transform.position);
                Destroy(platform.gameObject);

                if (platformExplosionGap > 0f) yield return new WaitForSeconds(platformExplosionGap);
            }
        }

        if (endScreenDelay > 0f) yield return new WaitForSeconds(endScreenDelay);

        // 3. and the run ends on the same travel screen the stops use
        ShowEndScreen(victory: false);
    }

    /// <summary>
    /// Every module placed on the city, from the top of the caravan downwards - the death
    /// sequence blows them up in that order (snapshot: they get destroyed).
    /// </summary>
    List<Module> CollectModules()
    {
        var modules = new List<Module>();
        if (city == null) return modules;

        foreach (var platform in city.Platforms)
        {
            if (platform == null) continue;

            foreach (var module in platform.Modules)
            {
                if (module != null) modules.Add(module);
            }
        }

        // Highest module first, so the explosions run top to bottom.
        modules.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));

        return modules;
    }

    static void Explode(ExplosionFX fx, Vector3 position)
    {
        if (fx == null) return;

        fx.Play(position);
    }

    /// <summary>
    /// The travel screen with the end message on top: the route bar at the progress of the run,
    /// the message ("run complete" or "city destroyed") and the restart button.
    /// </summary>
    void ShowEndScreen(bool victory)
    {
        if (travel == null)
        {
            Debug.LogWarning("[Battle] No travel transition in the scene - no end screen");
            return;
        }

        // Defeat: the bar stops at the node the player actually reached. The battle of the
        // current stop is fought on the way to it, so that node was never arrived at.
        float reached = NodeProgress(Mathf.Max(0, stopIndex - 1));
        float progress = victory ? 1f : reached;

        // Victory: the bar is already complete. Defeat: it fills up to the stop the city reached,
        // which reads as "this is how far the run got".
        travel.PlayEnd(victory ? progress : 0f, progress, RestartRun, victory);
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

        BattleStarted?.Invoke();
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

        // The boss has no station: beating it ends the run on the end screen.
        if (isBossBattle)
        {
            EndRun();
            return;
        }

        PlayRouteScreen();
    }

    /// <summary>
    /// The route screen of the leg that was just fought: the foreground covers the world and the
    /// bar fills to the node the city is arriving at. Once it has slid away the city rolls up to
    /// the stop and brakes in front of its building.
    /// </summary>
    void PlayRouteScreen()
    {
        if (travel == null || !travel.CanPlay)
        {
            StartArrival();
            return;
        }

        Debug.Log("[Battle] Victory - route screen before the stop");

        // The world keeps rolling behind the panel (and after it, on the way to the stop).
        parallax?.ResumeScrolling(Mathf.Max(0f, travel.RampDuration));

        travel.Play(NodeProgress(stopIndex - 1f), NodeProgress(stopIndex), StartArrival);
    }

    /// <summary>
    /// Drives up to the stop: the stop building scrolls in with the ground and the brake is timed
    /// so the city comes to a halt right in front of it.
    /// </summary>
    void StartArrival()
    {
        Debug.Log($"[Battle] Driving up to the stop {stopIndex + 1}/{StopCount}");

        float coast = Mathf.Max(0f, arrivalDriveDuration);

        var building = SpawnStopBuilding();
        if (building != null) coast = PlaceStopBuilding(building, coast);

        Debug.Log(building != null
            ? $"[Battle] Coasting for {coast:F2}s while '{building.name}' rolls in"
            : $"[Battle] Coasting for {coast:F2}s before the stop animation");

        if (coast <= 0f) PlayStopAnimation();
        else coastDelay = DOVirtual.DelayedCall(coast, PlayStopAnimation);

        // The station of the stop slides in once everything is still.
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
        float spawnX = stopX + coast * speed + brakeDistance;
        building.transform.position = new Vector3(spawnX, start.y, start.z);

        stopBuildingX = stopX;

        Debug.Log($"[Battle] '{building.name}' rolls in from x {spawnX:F2} to x {stopX:F2} " +
                  $"at {speed:F2} u/s (brake {brakeDistance:F2})");

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

        // The world is really still now: put the building on the exact planned spot (frame timing
        // during the brake can leave it a few centimetres off) and report the resulting gap, so
        // the prefab and the gap are easy to tune.
        if (currentBuilding != null)
        {
            var position = currentBuilding.transform.position;
            currentBuilding.transform.position = new Vector3(stopBuildingX, position.y, position.z);

            float gap = currentBuilding.Art.min.x - CityRightEdge();
            Debug.Log($"[Battle] Stopped in front of '{currentBuilding.name}' (gap {gap:F2})");
        }

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
        if (isRunOver || isDefeated || isAdvancing) return;

        // A second click while the station slides away must not skip a stop.
        isAdvancing = true;

        // Closing the station first, so the shop slides away with the module panel.
        station?.CloseAll();

        StartCoroutine(DriveThenFight());
    }

    System.Collections.IEnumerator DriveThenFight()
    {
        // The station needs a beat to slide off the screen: the city only pulls away after it.
        if (departDelay > 0f) yield return new WaitForSeconds(departDelay);

        // The engine cranks up and the city rolls out of the station: the enemy only shows up
        // after a few seconds of driving, so the fight starts on the open road instead of in
        // the parking lot.
        G.Get<AudioManager>()?.PlayEngineStart();
        parallax?.ResumeScrolling(travel != null ? travel.RampDuration : 0.4f);

        if (driveBeforeBattle > 0f)
        {
            Debug.Log($"[Battle] Rolling out - the enemy shows up in {driveBeforeBattle:F1}s");
            yield return new WaitForSeconds(driveBeforeBattle);
        }

        isAdvancing = false;
        AdvanceAfterStop();
    }

    /// <summary>
    /// Position on the travel bar, 0..1, in node units: 0 = the first node of the run (the start
    /// camp), StopCount = the boss. A travel fills exactly one node gap: when a leg is done the
    /// bar stands at the node the city just reached.
    /// </summary>
    float NodeProgress(float nodeIndex)
    {
        int nodes = StopCount + 1;
        if (nodes <= 0) return 0f;

        return Mathf.Clamp01(nodeIndex / StopCount);
    }

    void OnDestroy()
    {
        // Unlinked DOTween delays outlive the object, so drop them explicitly.
        spawnDelay?.Kill();
        ClearVictorySequence();

        if (city != null) city.Destroyed -= OnCityDestroyed;
    }
}
