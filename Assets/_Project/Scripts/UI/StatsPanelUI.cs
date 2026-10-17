using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Bottom-left panel that lists the city's current stats.
/// Stats whose value is 0 are hidden; the list refreshes when modules change.
/// </summary>
public class StatsPanelUI : MonoBehaviour
{
    [Tooltip("Inactive label used as a template for every stat row.")]
    [SerializeField] TMP_Text rowTemplate;

    readonly Dictionary<string, TMP_Text> rows = new();

    CityController city;

    void Start()
    {
        city = G.Get<CityController>();

        if (city == null || rowTemplate == null)
        {
            Debug.LogWarning("[StatsPanel] City or row template is missing");
            return;
        }

        rowTemplate.gameObject.SetActive(false);
        city.ModulesChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (city != null) city.ModulesChanged -= Refresh;
    }

    public void Refresh()
    {
        if (city == null) return;

        var stats = city.Stats;

        Show("ATK", stats.atack);
        Show("HP", stats.health);
        Show("DEF", stats.def);
        Show("SPD", stats.speed);
        Show("REGEN", stats.regen);
        Show("STUN", stats.stanChance);
    }

    void Show(string label, int value)
    {
        if (!rows.TryGetValue(label, out var row))
        {
            row = Instantiate(rowTemplate, rowTemplate.transform.parent);
            rows[label] = row;
        }

        // Zero stats are not interesting - keep them hidden.
        row.gameObject.SetActive(value != 0);
        row.text = $"{label} {value}";
    }
}
