using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Drives the gameplay loop: spawn an encounter -> fight until every enemy is dead ->
/// victory coast (the world keeps rolling for a moment) -> the parallax brakes to a
/// stop and the city shakes -> the station of the current map node opens (module panel
/// plus whatever else the node lists, e.g. the shop) -> "Next" -> the next encounter.
/// </summary>
public class BattleCycle : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    public EncounterData startingEncounter;

    [Tooltip("Map node the game starts on. When empty, the first MapNode in Resources/Map is used.")]
    [SerializeField] MapNode startingNode;

    [Header("Victory animation")]
    [Tooltip("How long the city keeps rolling at full speed after the last enemy dies, " +
             "before the brake starts. Gives the victory a moment to breathe.")]
    [Range(0f, 5f)]
    [SerializeField] float victoryCoastDuration = 3f;

    [Tooltip("How long the parallax takes to come to a full stop.")]
    [SerializeField] float stopDuration = 1.8f;

    [SerializeField] float moduleShakeAmplitude = 0.06f;
    [SerializeField] float platformShakeAmplitude = 0.08f;

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

    // Victory sequence: coast (world keeps rolling) -> brake + shake -> station.
    Tween coastDelay;
    Tween panelDelay;

    static MapNode defaultNode;

    /// <summary>
    /// Map node the city is currently at. The map sets this when it moves between nodes;
    /// the station that opens after a stop comes from it.
    /// </summary>
    public MapNode CurrentNode { get; set; }

    /// <summary>The node in use: an explicitly set one, the starting node, or a Resources fallback.</summary>
    public MapNode Node
    {
        get
        {
            if (CurrentNode != null) return CurrentNode;
            if (startingNode != null) return startingNode;

            if (defaultNode == null)
            {
                var nodes = Resources.LoadAll<MapNode>(MapNode.ResourceFolder);
                if (nodes.Length > 0) defaultNode = nodes[0];
            }

            return defaultNode;
        }
    }

    /// <summary>Encounter of the current node (or the authored starting encounter).</summary>
    public EncounterData CurrentEncounter
    {
        get
        {
            var node = Node;
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
        StartEncounter(CurrentEncounter);
    }

    /// <summary>Moves the city to a map node: fights its encounter and opens its station afterwards.</summary>
    public void EnterNode(MapNode node)
    {
        CurrentNode = node;

        station?.CloseAll();
        StartEncounter(CurrentEncounter);
    }

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

        Debug.Log($"[Battle] Victory - coasting for {coast:F2}s before the stop animation");

        if (coast <= 0f) PlayStopAnimation();
        else coastDelay = DOVirtual.DelayedCall(coast, PlayStopAnimation);

        // The panel slides in once everything is still.
        panelDelay = DOVirtual.DelayedCall(coast + stopDuration, ShowStation);
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
    /// Opens the station of the current map node: the module panel plus whatever else the
    /// node lists (the shop on a normal stop). Falls back to the module panel alone.
    /// </summary>
    void ShowStation()
    {
        panelDelay = null;

        if (station != null && Node != null)
        {
            Debug.Log($"[Battle] Station of '{Node.displayName}': {string.Join(", ", Node.stationPanels)}");
            station.Open(Node);
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
        // Closing the station first, so the shop slides away with the module panel.
        station?.CloseAll();

        StartEncounter(CurrentEncounter);
    }

    void OnDestroy()
    {
        // Unlinked DOTween delays outlive the object, so drop them explicitly.
        spawnDelay?.Kill();
        ClearVictorySequence();
    }
}
