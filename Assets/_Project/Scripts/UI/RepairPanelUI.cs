using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Repair station: a few buttons that patch the city up - for free or for resources.
/// <para>
/// The buttons are built from <see cref="actions"/>, so adding another repair option
/// ("restore a little", "restore more", later something else) is an inspector edit, not a
/// code change. Like the shop the panel sits where it is placed in the scene and slides off
/// screen while the station is closed (<see cref="SlidingStationPanel"/>).
/// </para>
/// </summary>
public class RepairPanelUI : SlidingStationPanel
{
    /// <summary>One repair option: what the button says and what it does.</summary>
    [Serializable]
    public class RepairAction
    {
        [Tooltip("Text on the button.")]
        public string label = "Restore durability";

        [Tooltip("Durability (city health) restored by this action.")]
        [Min(0)]
        public int amount = 25;

        [Tooltip("Cost; 0 = free.")]
        [Min(0)]
        public int price = 0;

        [Tooltip("Resource the cost is paid with.")]
        public ResourceType resource = ResourceType.ScrapMetal;
    }

    [Header("Actions")]
    [Tooltip("Buttons of the panel, top to bottom. Add entries to add more options.")]
    [SerializeField] List<RepairAction> actions = new()
    {
        new RepairAction { label = "Restore a little durability", amount = 25, price = 0 },
        new RepairAction { label = "Restore more durability", amount = 75, price = 12 },
    };

    [Header("Layout")]
    [Tooltip("Parent of the buttons. Empty = the panel itself.")]
    [SerializeField] RectTransform buttonContainer;

    [SerializeField] Vector2 buttonSize = new(104f, 22f);
    [SerializeField] float buttonSpacing = 6f;
    [SerializeField] float buttonsOffsetY = -4f;
    [SerializeField] Color buttonColor = new(1f, 0.95f, 0.85f, 0.16f);

    [Header("Look")]
    [Tooltip("Font of the generated labels. Falls back to the TMP default font.")]
    [SerializeField] TMP_FontAsset font;

    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color priceColor = new(1f, 0.85f, 0.5f, 1f);
    [SerializeField] Color disabledColor = new(1f, 1f, 1f, 0.35f);

    readonly List<RepairButton> buttons = new();

    TMP_Text title;
    bool subscribed;

    /// <summary>Buttons currently shown (same order as <see cref="actions"/>).</summary>
    public IReadOnlyList<RepairButton> Buttons => buttons;

    // ------------------------------------------------------------------ station API

    protected override void OnBeforeOpen(int level)
    {
        EnsureTitle();
        EnsureButtons();
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

    /// <summary>Runs one repair option (checks the city and the wallet first).</summary>
    public void Run(RepairAction action)
    {
        var city = G.Get<CityController>();
        var resources = G.Get<ResourceManager>();

        if (city == null || action == null) return;

        if (city.CurrentHealth >= city.Stats.health)
        {
            Debug.Log("[Repair] City is already at full durability");
            return;
        }

        if (action.price > 0 && resources != null && !resources.TrySpend(action.resource, action.price))
        {
            Debug.Log($"[Repair] Not enough {action.resource}: {resources.Get(action.resource)}/{action.price}");
            return;
        }

        city.Repair(action.amount);
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
            var action = button.Action;
            if (action == null) continue;

            bool affordable = action.price <= 0 ||
                              (resources != null && resources.Get(action.resource) >= action.price);
            bool usable = damaged && affordable;

            if (button.Button != null) button.Button.interactable = usable;
            if (button.Label != null) button.Label.color = usable ? textColor : disabledColor;

            if (button.Price != null)
            {
                button.Price.text = action.price <= 0 ? "FREE" : $"{action.price} {ShortName(action.resource)}";
                button.Price.color = usable ? priceColor : disabledColor;
            }
        }
    }

    // ------------------------------------------------------------------ setup

    void EnsureButtons()
    {
        if (buttons.Count == actions.Count && buttons.Count > 0)
        {
            // Keep the texts in sync when the list is edited at runtime.
            for (int i = 0; i < buttons.Count; i++) buttons[i].Label.text = actions[i].label;
            return;
        }

        foreach (var button in buttons)
        {
            if (button.Root != null) Destroy(button.Root);
        }

        buttons.Clear();

        var parent = buttonContainer != null ? buttonContainer : rect;
        int count = actions.Count;
        float step = buttonSize.y + buttonSpacing;
        float total = count * buttonSize.y + Mathf.Max(0, count - 1) * buttonSpacing;
        float startY = total * 0.5f - buttonSize.y * 0.5f + buttonsOffsetY;

        for (int i = 0; i < count; i++)
        {
            var button = CreateButton(parent, actions[i], i);
            ((RectTransform)button.Root.transform).anchoredPosition = new Vector2(0f, startY - i * step);

            buttons.Add(button);
        }
    }

    RepairButton CreateButton(RectTransform parent, RepairAction action, int index)
    {
        var rootObject = new GameObject($"Repair Action {index}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));

        var root = (RectTransform)rootObject.transform;
        root.SetParent(parent, false);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.localScale = Vector3.one;
        root.sizeDelta = buttonSize;

        var background = rootObject.GetComponent<Image>();
        background.color = buttonColor;
        background.raycastTarget = true;

        var button = rootObject.GetComponent<Button>();
        button.targetGraphic = background;

        // "Restore a little durability"
        var label = CreateText(root, "Label",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -2f), new Vector2(buttonSize.x - 8f, 10f), TextAlignmentOptions.Top, 7f);

        label.text = action.label;

        // "FREE" / "12 SCRAP"
        var price = CreateText(root, "Price",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 2f), new Vector2(buttonSize.x - 8f, 8f), TextAlignmentOptions.Bottom, 6f);

        price.color = priceColor;

        var data = new RepairButton(rootObject, button, label, price, action);

        button.onClick.AddListener(() => Run(data.Action));

        return data;
    }

    void EnsureTitle()
    {
        if (title != null) return;

        title = CreateText((RectTransform)transform, "Title",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -4f), new Vector2(100f, 12f), TextAlignmentOptions.Top, 9f);

        title.fontStyle = FontStyles.Bold;
        title.text = "REPAIR";
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

    TMP_Text CreateText(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 size, TextAlignmentOptions alignment, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var textRect = (RectTransform)go.transform;
        textRect.SetParent(parent, false);
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.pivot = pivot;
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = size;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = textColor;
        text.raycastTarget = false;
        text.text = string.Empty;

        return text;
    }

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

    /// <summary>One repair button on the panel.</summary>
    public sealed class RepairButton
    {
        public readonly GameObject Root;
        public readonly Button Button;
        public readonly TMP_Text Label;
        public readonly TMP_Text Price;
        public readonly RepairAction Action;

        public RepairButton(GameObject root, Button button, TMP_Text label, TMP_Text price, RepairAction action)
        {
            Root = root;
            Button = button;
            Label = label;
            Price = price;
            Action = action;
        }
    }
}
