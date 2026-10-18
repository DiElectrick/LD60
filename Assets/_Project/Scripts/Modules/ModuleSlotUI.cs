using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One inventory slot on the module panel. Shows the icon and the level of the generated
/// item it holds (level badge in the bottom-right corner), starts drags, and shows the
/// item tooltip while it is hovered with no mouse button pressed.
/// </summary>
public class ModuleSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Index in ModuleInventory.")]
    public int slotIndex;

    [Tooltip("Image that displays the module icon.")]
    public Image icon;

    [Tooltip("Label showing the item level in the bottom-right corner. Created automatically when empty.")]
    public TMP_Text levelText;

    [Tooltip("Font of the auto-created level label. Falls back to the TMP default font.")]
    [SerializeField] TMP_FontAsset levelFont;

    [Tooltip("Size of the auto-created level label.")]
    [SerializeField] float levelFontSize = 7f;

    /// <summary>Child name that is picked up automatically when <see cref="levelText"/> is empty.</summary>
    const string LevelLabelName = "Level";

    ModuleInventory inventory;
    ModuleDragController drag;

    bool isHovered;
    bool isPressed;

    Image background;
    Color backgroundTint;
    bool backgroundCaptured;

    void Start()
    {
        inventory = G.Get<ModuleInventory>();
        drag = FindAnyObjectByType<ModuleDragController>();

        EnsureLevelLabel();

        if (inventory != null) inventory.Changed += Refresh;

        Refresh();
    }

    void OnDisable()
    {
        isHovered = false;
        isPressed = false;

        ModuleTooltipUI.HideFrom(this);
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.Changed -= Refresh;
    }

    // ------------------------------------------------------------------ drag

    public void OnBeginDrag(PointerEventData eventData)
    {
        var item = inventory != null ? inventory.Get(slotIndex) : null;
        if (item == null || drag == null) return;

        ModuleTooltipUI.HideFrom(this);
        drag.Begin(item, this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        drag?.Move(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        drag?.End(eventData);
    }

    /// <summary>Clears the slot after the module was installed in the city.</summary>
    public void ConsumeModule()
    {
        inventory?.Take(slotIndex);
    }

    /// <summary>Tints the slot while a module carried from the city hovers over it.</summary>
    public void SetDropHighlight(bool highlighted, Color color = default)
    {
        if (!backgroundCaptured)
        {
            background = GetComponent<Image>();
            if (background == null) return;

            backgroundTint = background.color;
            backgroundCaptured = true;
        }

        if (background == null) return;

        background.color = highlighted ? color : backgroundTint;
    }

    // ------------------------------------------------------------------ hover / tooltip

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        // The tooltip only shows while no mouse button is held (never during a drag).
        if (isPressed || (eventData != null && eventData.pointerPress != null)) return;

        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        ModuleTooltipUI.HideFrom(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        ModuleTooltipUI.HideFrom(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;

        if (isHovered) ShowTooltip();
    }

    void ShowTooltip()
    {
        var item = inventory != null ? inventory.Get(slotIndex) : null;
        if (item == null || isPressed)
        {
            ModuleTooltipUI.HideIfAny();
            return;
        }

        ModuleTooltipUI.Instance?.Show(item, (RectTransform)transform, this);
    }

    // ------------------------------------------------------------------ view

    void Refresh()
    {
        var item = inventory != null ? inventory.Get(slotIndex) : null;

        if (icon != null)
        {
            icon.sprite = item != null ? item.Icon : null;
            icon.enabled = item != null;
        }

        if (levelText != null)
        {
            levelText.text = item != null ? $"Lv.{item.Level}" : string.Empty;
        }

        // The hovered item may have just been installed (slot is empty now).
        if (isHovered && !isPressed)
        {
            if (item != null) ShowTooltip();
            else ModuleTooltipUI.HideFrom(this);
        }
    }

    /// <summary>Finds the level label or creates one in the bottom-right corner of the slot.</summary>
    void EnsureLevelLabel()
    {
        if (levelText == null)
        {
            var existing = transform.Find(LevelLabelName);
            if (existing != null) levelText = existing.GetComponent<TMP_Text>();
        }

        if (levelText != null) return;

        var labelObject = new GameObject(LevelLabelName, typeof(RectTransform));
        var rect = (RectTransform)labelObject.transform;
        rect.SetParent(transform, false);

        // Bottom-right corner of the slot.
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(20f, 9f);
        rect.anchoredPosition = new Vector2(-1.5f, 1.2f);

        levelText = labelObject.AddComponent<TextMeshProUGUI>();
        levelText.font = levelFont != null ? levelFont : TMP_Settings.defaultFontAsset;
        levelText.fontSize = levelFontSize;
        levelText.fontStyle = FontStyles.Bold;
        levelText.alignment = TextAlignmentOptions.BottomRight;
        levelText.raycastTarget = false;
        levelText.text = string.Empty;
    }
}
