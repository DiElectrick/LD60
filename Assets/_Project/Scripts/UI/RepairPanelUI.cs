using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Repair station: the buttons are scene objects (<see cref="RepairButtonUI"/>), so they can
/// be placed, styled and duplicated by hand. The panel only collects them when it opens,
/// forwards their clicks and greys out what cannot be used right now.
/// <para>
/// Like the shop the panel sits where it is placed in the scene and slides off screen while
/// the station is closed (<see cref="SlidingStationPanel"/>).
/// </para>
/// </summary>
public class RepairPanelUI : SlidingStationPanel
{
    [Header("Look")]
    [Tooltip("Label colour of a button that can be used.")]
    [SerializeField] Color textColor = Color.white;

    [Tooltip("Price colour of a button that can be used.")]
    [SerializeField] Color priceColor = new(1f, 0.85f, 0.5f, 1f);

    [Tooltip("Colour of a button that cannot be used right now.")]
    [SerializeField] Color disabledColor = new(1f, 1f, 1f, 0.35f);

    readonly List<RepairButtonUI> buttons = new();

    bool subscribed;

    /// <summary>Repair buttons of the panel, in hierarchy order (collected when it opens).</summary>
    public IReadOnlyList<RepairButtonUI> Buttons => buttons;

    // ------------------------------------------------------------------ station API

    protected override void OnBeforeOpen(int level)
    {
        CollectButtons();
        Subscribe();
        Refresh();
    }

    protected override void OnBeforeClose()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    // ------------------------------------------------------------------ actions

    /// <summary>Runs one repair button (checks the city and the wallet first).</summary>
    public void Run(RepairButtonUI repairButton)
    {
        var city = G.Get<CityController>();
        var resources = G.Get<ResourceManager>();

        if (city == null || repairButton == null) return;

        G.Get<AudioManager>()?.PlaySfx("blipSelect");

        if (city.CurrentHealth >= city.Stats.health)
        {
            Debug.Log("[Repair] City is already at full durability");
            return;
        }

        if (repairButton.cost > 0 && resources != null &&
            !resources.TrySpend(repairButton.resource, repairButton.cost))
        {
            Debug.Log($"[Repair] Not enough {repairButton.resource}: " +
                      $"{resources.Get(repairButton.resource)}/{repairButton.cost}");
            G.Get<AudioManager>()?.PlaySfx("error");
            return;
        }

        city.Repair(repairButton.amount);
        Refresh();
    }

    /// <summary>Greys out buttons that cannot be used right now.</summary>
    public void Refresh()
    {
        var city = G.Get<CityController>();
        var resources = G.Get<ResourceManager>();

        bool damaged = city != null && city.CurrentHealth < city.Stats.health;

        foreach (var button in buttons)
        {
            if (button == null) continue;

            bool affordable = button.cost <= 0 ||
                              (resources != null && resources.Get(button.resource) >= button.cost);
            bool usable = damaged && affordable;

            if (button.Button != null) button.Button.interactable = usable;
            if (button.Title != null) button.Title.color = usable ? textColor : disabledColor;

            if (button.Price != null)
            {
                button.Price.text = button.cost <= 0 ? "FREE" : $"{button.cost} {ShortName(button.resource)}";
                button.Price.color = usable ? priceColor : disabledColor;
            }
        }
    }

    // ------------------------------------------------------------------ setup

    void CollectButtons()
    {
        buttons.Clear();
        buttons.AddRange(GetComponentsInChildren<RepairButtonUI>(true));

        foreach (var button in buttons)
        {
            if (button != null) button.Bind(this);
        }

        if (buttons.Count == 0)
            Debug.LogWarning("[Repair] The repair panel has no RepairButtonUI objects", this);
    }

    // ------------------------------------------------------------------ resources

    void Subscribe()
    {
        if (subscribed) return;

        var resources = G.Get<ResourceManager>();
        if (resources == null) return;

        resources.Changed += OnResourcesChanged;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed) return;

        var resources = G.Get<ResourceManager>();
        if (resources != null) resources.Changed -= OnResourcesChanged;

        subscribed = false;
    }

    void OnResourcesChanged(ResourceType type, int total)
    {
        Refresh();
    }

    // ------------------------------------------------------------------ helpers

    static string ShortName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.ScrapMetal: return "SCRAP";
            case ResourceType.Oil: return "OIL";
            case ResourceType.EnergyCore: return "CORE";
            default: return type.ToString();
        }
    }
}
