using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the gameplay loop: spawn an encounter -> fight until every enemy is dead ->
/// show the module panel -> wait for "Next" -> start the same encounter again.
/// </summary>
public class BattleCycle : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    public EncounterData startingEncounter;

    TurnManager turnManager;
    EnemyManager enemyManager;
    EnemySpawner spawner;
    ModulePanelUI modulePanel;

    EncounterData currentEncounter;
    bool isBattleRunning;

    public void Init()
    {
        turnManager = G.Get<TurnManager>();
        enemyManager = G.Get<EnemyManager>();
        spawner = G.Get<EnemySpawner>();

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
        StartEncounter(startingEncounter);
    }

    public void StartEncounter(EncounterData encounter)
    {
        if (encounter == null || encounter.enemies.Count == 0)
        {
            Debug.LogWarning("[Battle] No encounter to start");
            return;
        }

        currentEncounter = encounter;

        // 1. spawn the enemies described by the data
        var spawnedEnemies = spawner.SpawnEncounter(encounter.enemies);

        // 2. hand them to the turn order (the city is added by TurnManager itself)
        var turners = new List<ITurner>();
        foreach (var enemy in spawnedEnemies) turners.Add(enemy);

        turnManager.InitBattle(turners);
        isBattleRunning = true;

        Debug.Log($"[Battle] Started '{encounter.displayName}' with {spawnedEnemies.Count} enemies");
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
        spawner.DespawnAll();

        Debug.Log("[Battle] Victory - waiting for Next");

        if (modulePanel != null) modulePanel.Show();
        else StartEncounter(currentEncounter);
    }

    void OnNextPressed()
    {
        // For now the next battle is the same encounter again.
        StartEncounter(currentEncounter);
    }
}
