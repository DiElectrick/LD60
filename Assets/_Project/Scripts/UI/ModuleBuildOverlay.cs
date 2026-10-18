using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Build-mode (module placement) overlay for the city.
/// <para>
/// While the module panel is open, every installed module gets a level badge in the
/// bottom-right corner of its sprite, and hovering one with no mouse button pressed
/// shows the same tooltip the inventory uses (level + rolled stats).
/// </para>
/// The badges are ordinary UI labels positioned at the module's world position, so they
/// use the same font and scale as the rest of the UI.
/// </summary>
public class ModuleBuildOverlay : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    [Header("Level badges")]
    [Tooltip("Font of the build-mode level badges. Falls back to the TMP default font.")]
    [SerializeField] TMP_FontAsset font;

    [SerializeField] float labelSize = 8f;
    [SerializeField] Color labelColor = Color.white;

    [Tooltip("Offset from the bottom-right corner of the module sprite (world units).")]
    [SerializeField] Vector2 labelOffset = new(0.06f, -0.06f);

    [Header("Hover")]
    [Tooltip("Extra world units around the sprite that still count as hovering it.")]
    [SerializeField] float hoverPadding = 0.12f;

    CityController city;
    ModulePanelUI panel;
    ModuleDragController drag;
    Canvas canvas;
    RectTransform canvasRect;
    RectTransform labelLayer;

    readonly Dictionary<Module, TMP_Text> labels = new();

    bool buildMode;
    Module shownModule;

    System.Action onPanelShown;
    System.Action onPanelHidden;

    /// <summary>True while the placement phase is running (the module panel is open).</summary>
    public bool IsBuildMode => buildMode;

    public void Init()
    {
        city = G.Get<CityController>();
        drag = FindAnyObjectByType<ModuleDragController>(FindObjectsInactive.Include);
        panel = FindAnyObjectByType<ModulePanelUI>(FindObjectsInactive.Include);

        canvas = ScreenCanvas.Find();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        if (canvas == null) Debug.LogWarning("[BuildOverlay] No canvas - build-mode labels are disabled");

        if (city != null) city.ModulesChanged += RefreshLabels;

        if (panel != null)
        {
            onPanelShown = () => SetBuildMode(true);
            onPanelHidden = () => SetBuildMode(false);

            panel.Shown += onPanelShown;
            panel.Hidden += onPanelHidden;
        }
    }

    void OnDestroy()
    {
        if (city != null) city.ModulesChanged -= RefreshLabels;

        if (panel != null)
        {
            panel.Shown -= onPanelShown;
            panel.Hidden -= onPanelHidden;
        }

        foreach (var label in labels.Values)
        {
            if (label != null) Destroy(label.gameObject);
        }

        labels.Clear();
    }

    // ------------------------------------------------------------------ build mode

    /// <summary>Turns the overlay on/off (the module panel drives this).</summary>
    public void SetBuildMode(bool enabled)
    {
        if (buildMode == enabled) return;

        buildMode = enabled;
        RefreshLabels();

        if (!enabled) HideTooltip();
    }

    void Update()
    {
        if (!buildMode) return;

        // A module may have been destroyed (e.g. moved to the storage) - drop its label.
        PruneLabels();
        UpdateLabelPositions();

        // The drag controller owns the pointer while a module is being carried.
        if (drag != null && drag.IsDragging)
        {
            HideTooltip();
            return;
        }

        var mouse = Mouse.current;
        var cam = Camera.main;
        if (mouse == null || cam == null) return;

        bool pressed = mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed;
        Vector2 screen = mouse.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));

        // Never poke the world tooltip through the UI.
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (overUi)
        {
            HideTooltip();
            return;
        }

        // Press on an installed module: pick it up towards the storage.
        if (drag != null && mouse.leftButton.wasPressedThisFrame)
        {
            var candidate = ModuleAt(world);
            if (candidate != null && candidate.Instance != null)
            {
                HideTooltip();
                drag.BeginFromCity(candidate);
                return;
            }
        }

        UpdateHover(world, pressed);
    }

    /// <summary>
    /// Highlights the module under <paramref name="worldPoint"/> and shows its tooltip.
    /// Split out from <see cref="Update"/> so other pointer sources can drive it.
    /// </summary>
    public void UpdateHover(Vector3 worldPoint, bool buttonPressed)
    {
        if (!buildMode || buttonPressed)
        {
            HideTooltip();
            return;
        }

        var module = ModuleAt(worldPoint);
        if (module == null || module.Instance == null)
        {
            HideTooltip();
            return;
        }

        // Already showing this one - the tooltip re-places itself every frame.
        if (shownModule == module) return;

        var tooltip = ModuleTooltipUI.Instance;
        if (tooltip == null) return;

        shownModule = module;
        tooltip.ShowAtWorld(module.Instance, TooltipAnchor(module), this);
    }

    void HideTooltip()
    {
        shownModule = null;

        // Only close the tooltip this overlay opened - never one owned by an inventory slot.
        ModuleTooltipUI.HideFrom(this);
    }

    /// <summary>Installed module under a world point (nearest when sprites overlap), or null.</summary>
    public Module ModuleAt(Vector3 worldPoint)
    {
        if (city == null) return null;

        Module best = null;
        float bestDistance = float.MaxValue;

        foreach (var platform in city.Platforms)
        {
            foreach (var module in platform.Modules)
            {
                if (module == null) continue;

                var bounds = WorldBounds(module);
                bounds.Expand(hoverPadding * 2f);

                if (worldPoint.x < bounds.min.x || worldPoint.x > bounds.max.x) continue;
                if (worldPoint.y < bounds.min.y || worldPoint.y > bounds.max.y) continue;

                float distance = (bounds.center - worldPoint).sqrMagnitude;
                if (distance >= bestDistance) continue;

                best = module;
                bestDistance = distance;
            }
        }

        return best;
    }

    // ------------------------------------------------------------------ labels

    void RefreshLabels()
    {
        PruneLabels();

        var installed = new List<Module>();

        if (buildMode && city != null)
        {
            foreach (var platform in city.Platforms)
            {
                foreach (var module in platform.Modules)
                {
                    if (module == null) continue;

                    installed.Add(module);

                    var label = EnsureLabel(module);
                    label.text = $"Lv.{module.Level}";
                    label.gameObject.SetActive(true);
                }
            }
        }

        // Hide the badges of modules that are no longer installed (removed, stored, ...).
        foreach (var pair in labels)
        {
            if (pair.Value == null) continue;

            pair.Value.gameObject.SetActive(installed.Contains(pair.Key));
        }
    }

    /// <summary>Drops the labels of modules that have been destroyed.</summary>
    void PruneLabels()
    {
        List<Module> dead = null;

        foreach (var pair in labels)
        {
            if (pair.Key != null) continue;

            (dead ??= new List<Module>()).Add(pair.Key);
        }

        if (dead == null) return;

        foreach (var module in dead)
        {
            if (labels.TryGetValue(module, out var label) && label != null) Destroy(label.gameObject);

            labels.Remove(module);
        }
    }

    void UpdateLabelPositions()
    {
        foreach (var pair in labels)
        {
            var module = pair.Key;
            var label = pair.Value;

            if (module == null || label == null || !label.gameObject.activeSelf) continue;

            var rect = (RectTransform)label.transform;
            rect.anchoredPosition = CanvasPoint(ModuleCorner(module) + (Vector3)labelOffset);
        }
    }

    TMP_Text EnsureLabel(Module module)
    {
        if (labels.TryGetValue(module, out var existing) && existing != null) return existing;

        EnsureLabelLayer();

        var labelObject = new GameObject($"Lv {module.name}", typeof(RectTransform));
        var rect = (RectTransform)labelObject.transform;
        rect.SetParent(labelLayer, false);

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(30f, 12f);

        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.fontSize = labelSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.BottomRight;
        label.color = labelColor;
        label.raycastTarget = false;
        label.text = string.Empty;

        labels[module] = label;
        return label;
    }

    void EnsureLabelLayer()
    {
        if (labelLayer != null || canvas == null) return;

        var layerObject = new GameObject("ModuleLevelLabels", typeof(RectTransform));
        labelLayer = (RectTransform)layerObject.transform;
        labelLayer.SetParent(canvas.transform, false);
        labelLayer.anchorMin = Vector2.zero;
        labelLayer.anchorMax = Vector2.one;
        labelLayer.offsetMin = Vector2.zero;
        labelLayer.offsetMax = Vector2.zero;
        labelLayer.SetAsLastSibling();

        // The shop panel must cover the badges: keep the label layer *above* the shop in
        // the hierarchy sense (earlier siblings are drawn first, so they end up below).
        var shop = FindAnyObjectByType<ShopPanelUI>(FindObjectsInactive.Include);
        if (shop != null && shop.transform.parent == canvas.transform)
        {
            labelLayer.SetSiblingIndex(shop.transform.GetSiblingIndex());
        }
    }

    // ------------------------------------------------------------------ helpers

    Vector3 ModuleCorner(Module module)
    {
        var view = module.View;
        if (view == null) return module.transform.position;

        var bounds = view.bounds;
        return new Vector3(bounds.max.x, bounds.min.y, 0f);
    }

    Vector3 TooltipAnchor(Module module)
    {
        var view = module.View;
        if (view == null) return module.transform.position;

        var bounds = view.bounds;
        return new Vector3(bounds.center.x, bounds.max.y, 0f);
    }

    Bounds WorldBounds(Module module)
    {
        var view = module.View;
        if (view != null) return view.bounds;

        return new Bounds(module.transform.position, Vector3.one * 0.6f);
    }

    Vector2 CanvasPoint(Vector3 worldPosition)
    {
        if (canvasRect == null) return Vector2.zero;

        return canvasRect.InverseTransformPoint(worldPosition);
    }
}
