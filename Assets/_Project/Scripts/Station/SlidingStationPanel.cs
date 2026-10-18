using DG.Tweening;
using UnityEngine;

/// <summary>
/// A station panel that lives somewhere on screen and slides out beyond the screen edge
/// while the station is closed.
/// <para>
/// The panel is placed by hand in the scene - that position is where it appears. At the
/// start of the game (and after the station closes) it slides off screen, so the map node
/// decides when it comes back.
/// </para>
/// </summary>
public abstract class SlidingStationPanel : StationPanel
{
    /// <summary>Which screen edge the panel leaves through.</summary>
    public enum SlideSide
    {
        /// <summary>The nearest horizontal edge (left when the panel sits left of centre).</summary>
        Auto,

        Left,
        Right,
    }

    [Header("Slide")]
    [SerializeField] float slideDuration = 0.35f;

    [Tooltip("Extra distance kept outside the screen edge.")]
    [SerializeField] float edgeMargin = 6f;

    [Tooltip("Which edge the panel slides out through.")]
    [SerializeField] SlideSide slideSide = SlideSide.Auto;

    protected RectTransform rect;
    protected RectTransform canvasRect;
    protected Vector2 shownPosition;

    bool isOpen;

    public override bool IsOpen => isOpen;

    protected virtual void Awake()
    {
        EnsureSlide();
    }

    void Start()
    {
        // The canvas size is only reliable once the CanvasScaler has run, so the first
        // snap happens here instead of in Awake. Start runs before the first render,
        // so the panel never shows up in the middle of the screen.
        if (!isOpen) SnapHidden();
    }

    /// <summary>Captures the position the panel has in the scene and the canvas it lives on.</summary>
    protected void EnsureSlide()
    {
        if (rect != null) return;

        rect = (RectTransform)transform;
        shownPosition = rect.anchoredPosition;

        var canvas = ScreenCanvas.Find();
        canvasRect = canvas != null ? canvas.transform as RectTransform : null;
    }

    /// <summary>Where the panel is once it is out of view.</summary>
    protected Vector2 HiddenPosition()
    {
        return shownPosition + new Vector2(HiddenShift(), 0f);
    }

    float HiddenShift()
    {
        bool left = SlideLeft();

        if (canvasRect == null) return left ? -(rect.rect.width + 40f) : rect.rect.width + 40f;

        float halfWidth = canvasRect.rect.width * 0.5f;
        float halfPanel = rect.rect.width * 0.5f;

        if (left)
        {
            // The right edge of the panel ends up just outside the left screen edge.
            float rightEdge = shownPosition.x + halfPanel;
            float target = -halfWidth - edgeMargin;

            return Mathf.Min(0f, target - rightEdge);
        }

        // The left edge of the panel ends up just outside the right screen edge.
        float leftEdge = shownPosition.x - halfPanel;
        float targetEdge = halfWidth + edgeMargin;

        return Mathf.Max(0f, targetEdge - leftEdge);
    }

    bool SlideLeft()
    {
        switch (slideSide)
        {
            case SlideSide.Left: return true;
            case SlideSide.Right: return false;
            default: return shownPosition.x <= 0f;
        }
    }

    /// <summary>Jumps to the off-screen spot without animating (boot).</summary>
    protected void SnapHidden()
    {
        EnsureSlide();
        rect.DOKill();
        rect.anchoredPosition = HiddenPosition();
        isOpen = false;
    }

    public override void Open(int level)
    {
        EnsureSlide();

        // Prepare the content while the panel is still out of view.
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        OnBeforeOpen(level);

        rect.DOKill();
        rect.anchoredPosition = HiddenPosition();
        rect.DOAnchorPos(shownPosition, slideDuration).SetEase(Ease.OutCubic).SetLink(gameObject);

        isOpen = true;
    }

    public override void Close()
    {
        if (!isOpen && !gameObject.activeSelf) return;

        EnsureSlide();
        OnBeforeClose();

        isOpen = false;

        rect.DOKill();
        rect.DOAnchorPos(HiddenPosition(), slideDuration)
            .SetEase(Ease.InCubic)
            .SetLink(gameObject)
            .OnComplete(() => gameObject.SetActive(false));
    }

    /// <summary>Prepares the panel content before it slides in.</summary>
    protected abstract void OnBeforeOpen(int level);

    /// <summary>Clean-up when the station closes.</summary>
    protected virtual void OnBeforeClose()
    {
    }
}
