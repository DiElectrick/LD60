using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One row of the shop: a storage-style slot showing an item on sale.
/// Clicking it buys the item, hovering it shows the same tooltip the inventory uses.
/// </summary>
public class ShopOfferUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Icon of the offered module.")]
    [SerializeField] Image icon;

    [SerializeField] TMP_Text levelText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] Image priceIcon;
    [SerializeField] Button button;

    public Image Icon => icon;
    public TMP_Text Level => levelText;
    public TMP_Text Price => priceText;
    public Image PriceIcon => priceIcon;
    public Button Button => button;

    /// <summary>Item on sale (null after it was bought / when nothing was rolled).</summary>
    public ModuleInstance Item { get; set; }

    /// <summary>True once the item was sold - the row stops selling and stops showing a tooltip.</summary>
    public bool Sold { get; private set; }

    bool isHovered;
    bool isPressed;

    public void Bind(Image slotIcon, TMP_Text slotLevel, TMP_Text slotPrice, Image slotPriceIcon, Button slotButton)
    {
        icon = slotIcon;
        levelText = slotLevel;
        priceText = slotPrice;
        priceIcon = slotPriceIcon;
        button = slotButton;
    }

    public void SetSold(bool sold)
    {
        Sold = sold;

        if (sold) ModuleTooltipUI.HideFrom(this);
    }

    // ------------------------------------------------------------------ hover / tooltip

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

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

    void OnDisable()
    {
        isHovered = false;
        isPressed = false;

        ModuleTooltipUI.HideFrom(this);
    }

    void ShowTooltip()
    {
        if (Item == null || Sold)
        {
            ModuleTooltipUI.HideFrom(this);
            return;
        }

        ModuleTooltipUI.Instance?.Show(Item, (RectTransform)transform, this);
    }
}
