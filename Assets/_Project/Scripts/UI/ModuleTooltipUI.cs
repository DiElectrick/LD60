using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hover tooltip for generated modules: shows the item's level and every buff it rolled.
/// <para>
/// The panel (dark background + one rich-text label) is built in code when it is not
/// assigned in the inspector, so the tooltip works with no scene setup. Call it from the
/// slot that is hovered - the panel is placed above the slot and kept inside the canvas.
/// </para>
/// </summary>
public class ModuleTooltipUI : MonoBehaviour
{
    static ModuleTooltipUI instance;

    /// <summary>The tooltip of this scene (found, or created on the first slot that needs it).</summary>
    public static ModuleTooltipUI Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<ModuleTooltipUI>(FindObjectsInactive.Include);
            if (instance == null) instance = Create();

            return instance;
        }
    }

    /// <summary>Hides the tooltip when one exists - never creates one just to hide it.</summary>
    public static void HideIfAny()
    {
        if (instance != null) instance.Hide();
    }

    /// <summary>
    /// Hides the tooltip only when it was shown by <paramref name="source"/>, so one
    /// hover source (an inventory slot) never closes the tooltip of another (a module).
    /// </summary>
    public static void HideFrom(object source)
    {
        if (instance != null && ReferenceEquals(instance.Source, source)) instance.Hide();
    }

    [Header("Panel (optional - built automatically)")]
    [Tooltip("Background of the tooltip. Leave empty to let the component build its own.")]
    [SerializeField] RectTransform panel;

    [Tooltip("Label the tooltip text is written into.")]
    [SerializeField] TMP_Text label;

    [Header("Look")]
    [Tooltip("Panel width; the height follows the text.")]
    [SerializeField] float width = 120f;

    [SerializeField] float padding = 4f;

    [Tooltip("Body font size. The title is drawn two points larger.")]
    [SerializeField] float fontSize = 7f;

    [SerializeField] Color backgroundColor = new(0.04f, 0.05f, 0.08f, 0.94f);
    [SerializeField] Color titleColor = Color.white;
    [SerializeField] Color descriptionColor = new(0.72f, 0.72f, 0.68f, 1f);
    [SerializeField] Color mainStatColor = new(1f, 0.83f, 0.38f);
    [SerializeField] Color extraStatColor = new(0.65f, 0.84f, 1f);

    [Tooltip("Font used by the generated panel. Falls back to the TMP default font.")]
    [SerializeField] TMP_FontAsset font;

    [Header("Placement")]
    [Tooltip("Distance between the slot and the tooltip.")]
    [SerializeField] float gap = 3f;

    [Tooltip("Margin kept between the tooltip and the edge of the screen.")]
    [SerializeField] float screenMargin = 4f;

    Canvas canvas;
    RectTransform canvasRect;

    // What the tooltip follows: an inventory slot, or a module in the world (build mode).
    RectTransform anchorRect;
    bool followsWorldPoint;
    Vector3 anchorWorld;

    public bool IsVisible => panel != null && panel.gameObject.activeSelf;

    /// <summary>Object that asked for the tooltip that is currently shown (null when hidden).</summary>
    public object Source { get; private set; }

    /// <summary>Panel the tooltip is drawn in (built on demand when not assigned).</summary>
    public RectTransform Panel => panel;

    /// <summary>Label the tooltip text is written into.</summary>
    public TMP_Text Label => label;

    void Update()
    {
        // Follow the anchor while the panel slides in / the world scrolls past.
        if (!IsVisible) return;

        if (anchorRect != null) PlaceNear(CanvasPoint(anchorRect.position));
        else if (followsWorldPoint) PlaceNear(CanvasPoint(anchorWorld));
    }

    void Awake()
    {
        if (instance == null || instance == this) instance = this;

        EnsurePanel();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------------ public API

    /// <summary>Shows the item's buffs above <paramref name="anchor"/> (an inventory slot).</summary>
    public void Show(ModuleInstance item, RectTransform anchor, object source = null)
    {
        if (!Prepare(item, source)) return;

        anchorRect = anchor;
        followsWorldPoint = false;

        PlaceNear(CanvasPoint(anchor != null ? anchor.position : Vector3.zero));
    }

    /// <summary>Shows the item's buffs above a module in the world (build mode).</summary>
    public void ShowAtWorld(ModuleInstance item, Vector3 worldPoint, object source = null)
    {
        if (!Prepare(item, source)) return;

        anchorRect = null;
        followsWorldPoint = true;
        anchorWorld = worldPoint;

        PlaceNear(CanvasPoint(worldPoint));
    }

    /// <summary>Fills and measures the panel for one item; false when nothing can be shown.</summary>
    bool Prepare(ModuleInstance item, object source)
    {
        if (item == null)
        {
            Hide();
            return false;
        }

        EnsurePanel();
        if (panel == null || label == null)
        {
            Debug.LogWarning("[Tooltip] Panel is not set up - cannot show the tooltip");
            return false;
        }

        label.text = BuildText(item);
        Source = source;

        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();

        ResizeToText();
        return true;
    }

    public void Hide()
    {
        anchorRect = null;
        followsWorldPoint = false;
        Source = null;

        if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
    }

    /// <summary>World position to canvas-space point (the panel and the anchors share the canvas).</summary>
    Vector2 CanvasPoint(Vector3 worldPosition)
    {
        if (canvasRect == null) return Vector2.zero;

        return canvasRect.InverseTransformPoint(worldPosition);
    }

    // ------------------------------------------------------------------ content

    string BuildText(ModuleInstance item)
    {
        var text = new StringBuilder(128);

        // Title: "Gun Lv.2", a bit larger than the body.
        text.Append("<size=+2><b><color=#").Append(ColorUtility.ToHtmlStringRGB(titleColor)).Append(">");
        text.Append(item.DisplayName);
        text.Append("</color></b></size>");

        // What the module is for, in one short line.
        var description = item.Data != null ? item.Data.description : null;
        if (!string.IsNullOrWhiteSpace(description))
        {
            text.Append('\n');
            text.Append("<size=-1><color=#").Append(ColorUtility.ToHtmlStringRGB(descriptionColor)).Append(">");
            text.Append(description);
            text.Append("</color></size>");
        }

        foreach (var roll in item.Rolls)
        {
            text.Append('\n');

            var color = roll.isMain ? mainStatColor : extraStatColor;
            text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append(">");
            text.Append(roll.stat.ShortLabel());
            text.Append(roll.value >= 0 ? " +" : " -");
            text.Append(Mathf.Abs(roll.value));
            text.Append("</color>");
        }

        if (item.Rolls.Count == 0) text.Append("\n<size=-1><i>no bonuses</i></size>");

        return text.ToString();
    }

    void ResizeToText()
    {
        float contentWidth = Mathf.Max(1f, width - padding * 2f);

        // Give the label the final width so TMP can wrap and report its height.
        label.rectTransform.sizeDelta = new Vector2(contentWidth, 0f);
        label.ForceMeshUpdate();

        float contentHeight = Mathf.Max(label.preferredHeight, fontSize);

        label.rectTransform.sizeDelta = new Vector2(contentWidth, contentHeight);
        label.rectTransform.anchoredPosition = new Vector2(padding, -padding);

        panel.sizeDelta = new Vector2(width, contentHeight + padding * 2f);
    }

    // ------------------------------------------------------------------ placement

    void PlaceNear(Vector2 point)
    {
        if (canvasRect == null || panel == null)
        {
            if (panel != null) panel.anchoredPosition = Vector2.zero;
            return;
        }

        Vector2 size = panel.sizeDelta;

        float halfWidth = canvasRect.rect.width * 0.5f;
        float halfHeight = canvasRect.rect.height * 0.5f;

        // Prefer above the slot; drop below it when there is no room at the top.
        bool above = point.y + gap + size.y <= halfHeight - screenMargin;

        panel.pivot = new Vector2(0.5f, above ? 0f : 1f);

        float x = point.x;
        float y = above ? point.y + gap : point.y - gap;

        // Keep the panel inside the canvas.
        x = ClampToRange(x, -halfWidth + screenMargin, halfWidth - screenMargin, size.x * 0.5f);

        if (above) y = Mathf.Min(y, halfHeight - screenMargin - size.y);
        else y = Mathf.Max(y, -halfHeight + screenMargin + size.y);
        y = Mathf.Clamp(y, -halfHeight + screenMargin, halfHeight - screenMargin);

        panel.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>Clamps a centre coordinate so a box of <paramref name="halfSize"/> stays inside.</summary>
    static float ClampToRange(float value, float min, float max, float halfSize)
    {
        float low = min + halfSize;
        float high = max - halfSize;

        // The panel is wider than the canvas - just centre it.
        if (low > high) return (min + max) * 0.5f;

        return Mathf.Clamp(value, low, high);
    }

    // ------------------------------------------------------------------ building

    void EnsurePanel()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = ScreenCanvas.Find();

        canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        if (panel == null) BuildPanel();
        if (label == null && panel != null) label = panel.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.raycastTarget = false;

        if (panel != null) panel.gameObject.SetActive(false);
    }

    /// <summary>Creates the default tooltip panel under the canvas (last sibling = on top).</summary>
    void BuildPanel()
    {
        if (canvas == null)
        {
            Debug.LogWarning("[Tooltip] No canvas in the scene - cannot show tooltips");
            return;
        }

        var root = new GameObject("ModuleTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetAsLastSibling();

        panel = (RectTransform)root.transform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.sizeDelta = new Vector2(width, 20f);

        var background = root.GetComponent<Image>();
        background.color = backgroundColor;
        background.raycastTarget = false; // never steal the pointer from the slot

        var textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(panel, false);

        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = new Vector2(padding, -padding);
        textRect.sizeDelta = new Vector2(width - padding * 2f, 10f);

        label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        label.richText = true;
        label.text = string.Empty;

        root.SetActive(false);
    }

    static ModuleTooltipUI Create()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[Tooltip] No canvas in the scene - cannot show tooltips");
            return null;
        }

        // Lives on the canvas object; Awake builds the panel.
        var tooltip = canvas.gameObject.AddComponent<ModuleTooltipUI>();
        Debug.Log("[Tooltip] Created a tooltip on the canvas");

        return tooltip;
    }
}
