using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// First-run tutorial: a hand that shows the player what to do next, one beat at a time.
/// <para>
/// The four beats of the first run are: install a module in the city, look at the stats panel,
/// press Next and watch the first fight resolve on its own. Every caption is a serialized field,
/// so the wording can be edited in the inspector without touching the code.
/// </para>
/// <para>
/// The overlay never blocks input - every image it builds ignores raycasts - so the player drags
/// and clicks the real UI right through it. It is shown once: <see cref="ShownKey"/> in PlayerPrefs
/// remembers that the first run already had it, and a reloaded scene (a new run) skips it.
/// </para>
/// </summary>
public class TutorialController : MonoBehaviour, IBootstraped
{
    /// <summary>PlayerPrefs key set to 1 once the tutorial was shown (only the first run gets it).</summary>
    public const string ShownKey = "LD60.TutorialShown";

    /// <summary>What the current step wants the player to do.</summary>
    enum Gesture
    {
        /// <summary>Nothing to point at - the step is just a caption.</summary>
        None,

        /// <summary>The hand taps the target.</summary>
        Tap,

        /// <summary>The hand travels from the target into the city (a drag).</summary>
        Drag,
    }

    /// <summary>Where the tutorial currently is; the step order lives here, not in a list.</summary>
    enum Phase
    {
        /// <summary>The tutorial is not running (already seen, or finished).</summary>
        Off,

        /// <summary>1. Drag a module out of the storage into the city.</summary>
        Build,

        /// <summary>2. Look at the stats panel the module just changed.</summary>
        Stats,

        /// <summary>3. Press Next to leave the station.</summary>
        Depart,

        /// <summary>Next was pressed - waiting for the first fight to start.</summary>
        Waiting,

        /// <summary>4. The first fight: it plays itself.</summary>
        Battle,

        /// <summary>All four steps are done.</summary>
        Done,
    }

    public bool isInitialised { get; set; }

    // ------------------------------------------------------------------ when

    [Header("When it runs")]
    [Tooltip("Show the tutorial only on the first run (PlayerPrefs). Off = every run.")]
    [SerializeField] bool onlyFirstRun = true;

    [Tooltip("Debug: show it even when it was already seen. Nothing is written to PlayerPrefs.")]
    [SerializeField] bool alwaysShow;

    [Tooltip("Delay before the first step, so the station panel has finished sliding in.")]
    [Min(0f)]
    [SerializeField] float startDelay = 0.9f;

    // ------------------------------------------------------------------ texts

    [Header("Step texts (English)")]
    [Tooltip("1. Shown while the player has to drag a module into the city.")]
    [TextArea(1, 3)]
    [SerializeField] string buildText = "Build your city from modules";

    [Tooltip("2. Shown while the stats panel is highlighted.")]
    [TextArea(1, 3)]
    [SerializeField] string statsText = "Modules affect stats";

    [Tooltip("3. Shown while the hand points at the Next button.")]
    [TextArea(1, 3)]
    [SerializeField] string departText = "Set out on your journey";

    [Tooltip("4. Shown when the first fight begins.")]
    [TextArea(1, 3)]
    [SerializeField] string battleText = "Combat is automatic";

    // ------------------------------------------------------------------ rhythm

    [Header("Rhythm")]
    [Tooltip("How long the stats step stays up before the hand moves on to Next.")]
    [Min(0f)]
    [SerializeField] float statsHoldTime = 4f;

    [Tooltip("How long the final caption stays up before the tutorial disappears for good.")]
    [Min(0f)]
    [SerializeField] float battleHoldTime = 5f;

    [Tooltip("How long one lap of the dragging hand takes, in seconds: the travel plus the pause " +
             "while the hand is gone.")]
    [Min(0.1f)]
    [SerializeField] float dragPeriod = 1.8f;

    [Tooltip("Part of the lap the hand spends travelling; the rest it is invisible and then " +
             "reappears at the module.")]
    [Range(0.2f, 1f)]
    [SerializeField] float dragTravelShare = 0.6f;

    // ------------------------------------------------------------------ targets

    [Header("Targets (left empty = found automatically)")]
    [Tooltip("Slot the hand drags from. Empty = the first storage slot holding an item.")]
    [SerializeField] RectTransform buildTarget;

    [Tooltip("Panel highlighted by step 2. Empty = the stats panel of the HUD.")]
    [SerializeField] RectTransform statsTarget;

    [Tooltip("Button pointed at by step 3. Empty = the module panel's Next button.")]
    [SerializeField] RectTransform departTarget;

    [Tooltip("World object the hand drags towards (a platform column). Empty = the first platform.")]
    [SerializeField] Transform dragTarget;

    // ------------------------------------------------------------------ hand

    [Header("Hand")]
    [Tooltip("Pointer sprite (the Export_26 hand of the art sheet).")]
    [SerializeField] Sprite handSprite;

    [Tooltip("Size of the hand on the canvas, in canvas units (the HUD is 384x216).")]
    [SerializeField] Vector2 handSize = new(28f, 22f);

    [Tooltip("Offset from the pointed-at spot, in canvas units. Y < 0 puts the hand under it.")]
    [SerializeField] Vector2 handOffset = new(0f, -18f);

    [Tooltip("Vertical bob of a tapping hand, in canvas units.")]
    [SerializeField] float handBob = 2.5f;

    [SerializeField] Color handColor = Color.white;

    // ------------------------------------------------------------------ caption

    [Header("Caption")]
    [Tooltip("Pixel font of the caption. Falls back to the TMP default font.")]
    [SerializeField] TMP_FontAsset font;

    [Tooltip("Width of the caption box, in canvas units.")]
    [SerializeField] float textWidth = 300f;

    [Tooltip("Font size of the caption.")]
    [SerializeField] float textFontSize = 9f;

    [Tooltip("Distance of the caption box from the bottom screen edge.")]
    [SerializeField] Vector2 textOffset = new(0f, 3f);

    [SerializeField] Color textColor = Color.white;

    [SerializeField] Color textBackground = new(0.04f, 0.05f, 0.08f, 0.92f);

    [Tooltip("Padding inside the caption box.")]
    [SerializeField] float textPadding = 4f;

    // ------------------------------------------------------------------ spotlight

    [Header("Spotlight")]
    [Tooltip("Colour of the screen dimmed around the highlighted area.")]
    [SerializeField] Color dimColor = new(0f, 0f, 0f, 0.55f);

    [Tooltip("Colour of the frame drawn around the highlighted area.")]
    [SerializeField] Color frameColor = new(1f, 0.84f, 0.36f, 1f);

    [Tooltip("Free space kept between the highlighted area and its frame.")]
    [SerializeField] float spotlightPadding = 3f;

    // runtime
    Canvas canvas;
    RectTransform canvasRect;
    RectTransform root;
    RectTransform textPanel;
    TMP_Text label;
    Image hand;
    Image[] dim;
    Image[] frame;

    CityController city;
    ModuleDragController dragController;
    ModulePanelUI modulePanel;
    BattleCycle battle;

    Phase phase = Phase.Off;
    RectTransform currentTarget;
    Gesture currentGesture;
    bool showSpotlight;
    Coroutine holdRoutine;

    // ------------------------------------------------------------------ lifecycle

    public void Init()
    {
        if (!ShouldShow())
        {
            Debug.Log("[Tutorial] Already shown once - skipping");
            return;
        }

        city = G.Get<CityController>();
        dragController = FindAnyObjectByType<ModuleDragController>(FindObjectsInactive.Include);
        modulePanel = FindAnyObjectByType<ModulePanelUI>(FindObjectsInactive.Include);
        battle = G.Get<BattleCycle>();

        if (!BuildOverlay()) return;

        if (modulePanel != null) modulePanel.OnNext += OnNextPressed;
        if (battle != null) battle.BattleStarted += OnBattleStarted;

        MarkShown();

        StartCoroutine(BeginRoutine());
    }

    void OnDestroy()
    {
        if (modulePanel != null) modulePanel.OnNext -= OnNextPressed;
        if (battle != null) battle.BattleStarted -= OnBattleStarted;
        if (dragController != null) dragController.ModuleInstalled -= OnModuleInstalled;

        PlayerPrefs.Save();
    }

    /// <summary>True when this run is allowed to see the tutorial.</summary>
    bool ShouldShow()
    {
        if (alwaysShow) return true;
        if (!onlyFirstRun) return true;

        return PlayerPrefs.GetInt(ShownKey, 0) == 0;
    }

    /// <summary>Remembers that the first run had the tutorial - a restarted run skips it.</summary>
    void MarkShown()
    {
        if (alwaysShow || !onlyFirstRun) return;

        PlayerPrefs.SetInt(ShownKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Forgets the saved progress, so the next run shows the tutorial again.</summary>
    [ContextMenu("Reset tutorial (show it on the next run)")]
    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey(ShownKey);
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] Progress reset - the tutorial will show up on the next run");
    }

    void Update()
    {
        if (root == null || !root.gameObject.activeSelf) return;

        // The player is doing the drag themselves - the hint would sit on top of the ghost.
        bool hiddenWhileDragging = phase == Phase.Build && dragController != null && dragController.IsDragging;
        if (hand != null) hand.enabled = !hiddenWhileDragging && currentGesture != Gesture.None && currentTarget != null;

        RefreshLayout();
    }

    // ------------------------------------------------------------------ steps

    IEnumerator BeginRoutine()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        ResolveTargets();
        ShowBuildStep();
    }

    /// <summary>Fills the targets that were left empty in the inspector with the HUD objects.</summary>
    void ResolveTargets()
    {
        if (buildTarget == null) buildTarget = FirstStorageSlot();
        if (statsTarget == null) statsTarget = FindStatsPanel();
        if (departTarget == null && modulePanel != null && modulePanel.nextButton != null)
            departTarget = (RectTransform)modulePanel.nextButton.transform;
        if (dragTarget == null && city != null && city.Platforms.Count > 0)
            dragTarget = city.Platforms[0].transform;
    }

    RectTransform FirstStorageSlot()
    {
        var slots = FindObjectsByType<ModuleSlotUI>(FindObjectsInactive.Include);
        var inventory = G.Get<ModuleInventory>();

        RectTransform first = null;

        foreach (var slot in slots)
        {
            if (slot == null) continue;

            var rect = (RectTransform)slot.transform;
            if (first == null) first = rect;

            // Prefer a slot that actually holds an item - that is the one to drag from.
            if (inventory != null && inventory.Get(slot.slotIndex) != null) return rect;
        }

        return first;
    }

    RectTransform FindStatsPanel()
    {
        var stats = FindAnyObjectByType<StatsPanelUI>(FindObjectsInactive.Include);
        return stats != null ? (RectTransform)stats.transform : null;
    }

    void ShowBuildStep()
    {
        phase = Phase.Build;

        if (dragController != null) dragController.ModuleInstalled += OnModuleInstalled;

        Debug.Log("[Tutorial] Step 1 - drag a module into the city");
        Show(buildText, buildTarget, Gesture.Drag, false);
    }

    void OnModuleInstalled(ModuleInstance item)
    {
        if (phase != Phase.Build) return;

        if (dragController != null) dragController.ModuleInstalled -= OnModuleInstalled;

        Debug.Log($"[Tutorial] Module installed '{item?.DisplayName}'");
        ShowStatsStep();
    }

    void ShowStatsStep()
    {
        phase = Phase.Stats;

        Debug.Log("[Tutorial] Step 2 - the stats panel changed");
        Show(statsText, statsTarget, Gesture.Tap, true);

        StartHold(statsHoldTime, () =>
        {
            if (phase == Phase.Stats) ShowDepartStep();
        });
    }

    void ShowDepartStep()
    {
        phase = Phase.Depart;

        Debug.Log("[Tutorial] Step 3 - press Next to leave the station");
        Show(departText, departTarget, Gesture.Tap, false);
    }

    void OnNextPressed()
    {
        if (phase == Phase.Off || phase == Phase.Waiting || phase == Phase.Battle || phase == Phase.Done) return;

        Debug.Log("[Tutorial] Next pressed - waiting for the first fight");

        StopHold();
        HideOverlay();
        phase = Phase.Waiting;
    }

    void OnBattleStarted()
    {
        if (phase == Phase.Off || phase == Phase.Battle || phase == Phase.Done) return;

        Debug.Log("[Tutorial] Step 4 - the first fight is running");

        StopHold();
        phase = Phase.Battle;

        Show(battleText, null, Gesture.None, false);

        StartHold(battleHoldTime, Finish);
    }

    void Finish()
    {
        if (phase == Phase.Done) return;

        phase = Phase.Done;

        StopHold();
        HideOverlay();

        if (dragController != null) dragController.ModuleInstalled -= OnModuleInstalled;

        Debug.Log("[Tutorial] Done");
    }

    void StartHold(float seconds, System.Action then)
    {
        StopHold();
        if (seconds <= 0f)
        {
            then();
            return;
        }

        holdRoutine = StartCoroutine(HoldRoutine(seconds, then));
    }

    IEnumerator HoldRoutine(float seconds, System.Action then)
    {
        yield return new WaitForSeconds(seconds);

        holdRoutine = null;
        then();
    }

    void StopHold()
    {
        if (holdRoutine == null) return;

        StopCoroutine(holdRoutine);
        holdRoutine = null;
    }

    // ------------------------------------------------------------------ showing

    /// <summary>Shows one step: caption, pointed-at spot and gesture.</summary>
    void Show(string text, RectTransform target, Gesture gesture, bool spotlight)
    {
        if (root == null) return;

        SetText(text);

        currentTarget = target;
        currentGesture = gesture;
        showSpotlight = spotlight && target != null;

        foreach (var line in frame) line.enabled = showSpotlight;

        if (hand != null)
        {
            hand.sprite = handSprite;
            hand.enabled = gesture != Gesture.None && target != null;
        }

        root.gameObject.SetActive(true);
        root.SetAsLastSibling();

        RefreshLayout();
    }

    void HideOverlay()
    {
        currentTarget = null;
        currentGesture = Gesture.None;
        showSpotlight = false;

        if (root != null) root.gameObject.SetActive(false);
    }

    /// <summary>Re-places the hand and the spotlight on the target of the current step.</summary>
    void RefreshLayout()
    {
        if (canvasRect == null || canvas == null) return;

        if (currentTarget == null)
        {
            SetDimVisible(false);
            return;
        }

        Rect hole = CanvasRectOf(currentTarget);
        Rect padded = Rect.MinMaxRect(
            hole.xMin - spotlightPadding, hole.yMin - spotlightPadding,
            hole.xMax + spotlightPadding, hole.yMax + spotlightPadding);

        SetDimVisible(showSpotlight);
        if (showSpotlight) LayoutDim(padded);
        if (showSpotlight) LayoutFrame(padded);

        if (hand == null || !hand.enabled) return;

        Vector2 position = hole.center;

        if (currentGesture == Gesture.Drag)
        {
            PlaceDraggingHand(position);
            return;
        }

        position.y += Mathf.Sin(Time.unscaledTime * 6f) * handBob;
        ((RectTransform)hand.transform).anchoredPosition = position + handOffset;
    }

    /// <summary>
    /// The dragging hand: it travels from the module to the city, vanishes for a beat and then
    /// starts over at the module - it never shuttles back through the city.
    /// </summary>
    void PlaceDraggingHand(Vector2 start)
    {
        var handRect = (RectTransform)hand.transform;

        if (dragTarget == null || !TryCanvasPoint(dragTarget.position, out var end))
        {
            handRect.anchoredPosition = start + handOffset;
            return;
        }

        float lap = Mathf.Max(0.1f, dragPeriod);
        float travel = Mathf.Clamp(dragTravelShare, 0.2f, 1f);
        float phase = Mathf.Repeat(Time.unscaledTime / lap, 1f);

        // The tail of the lap is the pause: the hand is gone, and the next lap starts at the module.
        if (phase >= travel)
        {
            hand.enabled = false;
            return;
        }

        float t = Mathf.SmoothStep(0f, 1f, phase / travel);
        handRect.anchoredPosition = Vector2.Lerp(start, end, t) + handOffset;
    }

    // ------------------------------------------------------------------ geometry

    /// <summary>Rect of a UI element in canvas-local units (the canvas pivot is the origin).</summary>
    Rect CanvasRectOf(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        foreach (var corner in corners)
        {
            Vector2 point = canvasRect.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>World point (a platform) as a canvas-local point, or false when there is no camera.</summary>
    bool TryCanvasPoint(Vector3 worldPoint, out Vector2 point)
    {
        point = Vector2.zero;

        var cam = Camera.main;
        if (cam == null) return false;

        Vector2 screenPoint = cam.WorldToScreenPoint(worldPoint);
        Camera projection = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, projection, out point);
    }

    // ------------------------------------------------------------------ spotlight

    void SetDimVisible(bool visible)
    {
        foreach (var quad in dim)
        {
            if (quad != null) quad.enabled = visible;
        }

        if (!visible)
        {
            foreach (var line in frame)
            {
                if (line != null) line.enabled = false;
            }
        }
    }

    /// <summary>Dims everything but the highlighted rectangle (four quads around the hole).</summary>
    void LayoutDim(Rect hole)
    {
        float w = canvasRect.rect.width;
        float h = canvasRect.rect.height;

        float xMin = Mathf.Clamp(hole.xMin + w * 0.5f, 0f, w);
        float xMax = Mathf.Clamp(hole.xMax + w * 0.5f, 0f, w);
        float yMin = Mathf.Clamp(hole.yMin + h * 0.5f, 0f, h);
        float yMax = Mathf.Clamp(hole.yMax + h * 0.5f, 0f, h);

        SetBox(dim[0], 0f, yMax, w, h - yMax);          // above
        SetBox(dim[1], 0f, 0f, w, yMin);                // below
        SetBox(dim[2], 0f, yMin, xMin, yMax - yMin);    // left
        SetBox(dim[3], xMax, yMin, w - xMax, yMax - yMin); // right
    }

    /// <summary>Thin line around the highlighted rectangle.</summary>
    void LayoutFrame(Rect hole)
    {
        float w = canvasRect.rect.width;
        float h = canvasRect.rect.height;
        float thickness = 1f;

        float xMin = Mathf.Clamp(hole.xMin + w * 0.5f, 0f, w);
        float xMax = Mathf.Clamp(hole.xMax + w * 0.5f, 0f, w);
        float yMin = Mathf.Clamp(hole.yMin + h * 0.5f, 0f, h);
        float yMax = Mathf.Clamp(hole.yMax + h * 0.5f, 0f, h);

        SetBox(frame[0], xMin, yMax - thickness, xMax - xMin, thickness); // top
        SetBox(frame[1], xMin, yMin, xMax - xMin, thickness);             // bottom
        SetBox(frame[2], xMin, yMin, thickness, yMax - yMin);             // left
        SetBox(frame[3], xMax - thickness, yMin, thickness, yMax - yMin); // right
    }

    /// <summary>Places a quad by its bottom-left corner in canvas units (canvas bottom-left = 0,0).</summary>
    static void SetBox(Image image, float x, float y, float width, float height)
    {
        if (image == null) return;

        var rect = (RectTransform)image.transform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    // ------------------------------------------------------------------ building

    /// <summary>Builds the overlay (hand, caption, dim quads, frame) under the screen canvas.</summary>
    bool BuildOverlay()
    {
        canvas = ScreenCanvas.Find();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        if (canvasRect == null)
        {
            Debug.LogWarning("[Tutorial] No screen canvas - the tutorial cannot be shown");
            return false;
        }

        var rootObject = new GameObject("TutorialOverlay", typeof(RectTransform));
        root = (RectTransform)rootObject.transform;
        root.SetParent(canvasRect, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);

        dim = new Image[4];
        for (int i = 0; i < dim.Length; i++)
        {
            dim[i] = CreateQuad("Dim " + i, dimColor);
            dim[i].enabled = false;
        }

        frame = new Image[4];
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] = CreateQuad("Frame " + i, frameColor);
            frame[i].enabled = false;
        }

        hand = CreateQuad("Hand", handColor);
        hand.sprite = handSprite;
        hand.preserveAspect = true;
        var handRect = (RectTransform)hand.transform;
        handRect.anchorMin = new Vector2(0.5f, 0.5f);
        handRect.anchorMax = new Vector2(0.5f, 0.5f);
        handRect.pivot = new Vector2(0.5f, 0.5f);
        handRect.sizeDelta = handSize;
        hand.enabled = false;

        BuildCaption();

        root.gameObject.SetActive(false);
        return true;
    }

    void BuildCaption()
    {
        var panelObject = new GameObject("Caption", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(root, false);

        textPanel = (RectTransform)panelObject.transform;
        textPanel.anchorMin = new Vector2(0.5f, 0f);
        textPanel.anchorMax = new Vector2(0.5f, 0f);
        textPanel.pivot = new Vector2(0.5f, 0f);
        textPanel.anchoredPosition = textOffset;
        textPanel.sizeDelta = new Vector2(textWidth, 20f);

        var background = panelObject.GetComponent<Image>();
        background.color = textBackground;
        background.raycastTarget = false;

        var labelObject = new GameObject("Text", typeof(RectTransform));
        labelObject.transform.SetParent(textPanel, false);

        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(textPadding, -textPadding);
        labelRect.sizeDelta = new Vector2(textWidth - textPadding * 2f, 10f);

        label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.fontSize = textFontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.richText = true;
        label.text = string.Empty;
    }

    /// <summary>Fits the caption box to the text that was just written into it.</summary>
    void SetText(string text)
    {
        if (label == null || textPanel == null) return;

        label.text = text;

        float contentWidth = Mathf.Max(10f, textWidth - textPadding * 2f);
        var labelRect = (RectTransform)label.transform;

        // Give the label its final width first, so TMP can wrap and report the height.
        labelRect.sizeDelta = new Vector2(contentWidth, 0f);
        label.ForceMeshUpdate();

        float height = Mathf.Max(label.preferredHeight, textFontSize);
        labelRect.sizeDelta = new Vector2(contentWidth, height);
        textPanel.sizeDelta = new Vector2(textWidth, height + textPadding * 2f);
    }

    /// <summary>A full-size transparent quad used for the dim and for the hand.</summary>
    Image CreateQuad(string name, Color color)
    {
        var quadObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        quadObject.transform.SetParent(root, false);

        var rect = (RectTransform)quadObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = quadObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }
}
