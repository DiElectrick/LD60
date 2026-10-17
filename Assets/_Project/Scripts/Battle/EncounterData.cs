using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data for one battle: which enemies spawn and in what order.
/// Create with Assets > Create > LD60 > Encounter Data.
/// </summary>
[CreateAssetMenu(menuName = "LD60/Encounter Data", fileName = "EncounterData")]
public class EncounterData : ScriptableObject
{
    public string displayName = "Encounter";

    public List<EnemyData> enemies = new();
}
