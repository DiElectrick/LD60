using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Station shop: while the city is stopped it offers a few generated modules for resources.
/// <para>
/// Every offer is a slot that looks like a storage slot (prefab: Resources/UI/ShopSlot),
/// with the price (resource icon + amount) next to it. Clicking a slot buys the item,
/// hovering it shows its tooltip.
/// </para>
/// There is no show/hide button: the panel sits where it is placed in the scene and slides
/// off screen (see <see cref="SlidingStationPanel"/>), coming back when its map node opens
/// the station.
/// </summary>
public class ShopPanelUI : SlidingStationPanel
{
    [Header("Offers")]
    [Tooltip("How many modules the shop offers.")]
    [SerializeField] int offerCount = 3;

    [Tooltip("Parent of the offer slots. Empty = the panel itself.")]
    [SerializeField] RectTransform offerContainer;

    [Tooltip("Vertical gap between two offers.")]
    [SerializeField] float rowSpacing = 10f;

    [Tooltip("Vertical shift of the whole offer block.")]
    [SerializeField] float offersOffsetY = -4f;

    [Tooltip("Horizontal shift of the offer column (the price sits to the right of the slot).")]
    [SerializeField] float offersOffsetX = -6f;

    [Header("Slot look")]
    [Tooltip("Slot prefab the offers are instantiated from. Empty = Resources/UI/ShopSlot.")]
    [SerializeField] ShopOfferUI slotPrefab;

    [Tooltip("Slot background sprite used only when no prefab is set. Empty = copied from an inventory slot.")]
    [SerializeField] Sprite slotSprite;

    [Tooltip("Size of an offer slot when it is built in code (the prefab keeps its own size).")]
    [SerializeField] Vector2 slotSize = new(40f, 25f);

    [Tooltip("Gap between the slot and its price, code-built slots only.")]
    [SerializeField] float priceGap = 6f;

    [Tooltip("Size of the resource icon next to the price, code-built slots only.")]
    [SerializeField] float priceIconSize = 9f;

    [Header("Levels")]
    [Tooltip("Item level the first shop of the run offers.")]
    [Min(1)]
    [SerializeField] int baseLevel = 1;

    [Tooltip("How many stops it takes before the shop offers one level higher - the shop gets " +
             "better as the run goes on.")]
    [Min(1)]
    [SerializeField] int stopsPerLevel = 2;

    [Tooltip("Random level spread around the shop's level. 0 = exactly the shop level.")]
    [Range(0, 2)]
    [SerializeField] int levelVariance = 0;

    [Header("Price")]
    [Tooltip("Cost per item level. 0 = the shop is free.")]
    [Min(0)]
    [SerializeField] int pricePerLevel = 3;

    [SerializeField] ResourceType priceResource = ResourceType.ScrapMetal;

    [Header("Look")]
    [Tooltip("Font of the generated labels. Falls back to the TMP default font.")]
    [SerializeField] TMP_FontAsset font;

    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color priceColor = new(1f, 0.85f, 0.5f, 1f);

    readonly List<ShopOfferUI> offers = new();
    readonly Dictionary<ResourceType, Sprite> priceIcons = new();

    TMP_Text title;
    int openLevel;

    /// <summary>Where the slot prefab is loaded from when it is not assigned.</summary>
    const string SlotPrefabResourcePath = "UI/ShopSlot";

    /// <summary>Slots currently offered (sold ones stay listed, marked as sold).</summary>
    public IReadOnlyList<ShopOfferUI> Offers => offers;

    // ------------------------------------------------------------------ station API

    protected override void OnBeforeOpen(int level)
    {
        openLevel = level > 0 ? level : baseLevel;

        EnsureRows();
        EnsureTitle();
        RollOffers();
    }

    // ------------------------------------------------------------------ offers

    void RollOffers()
    {
        var generator = ModuleGenerator.Instance;
        if (generator == null)
        {
            Debug.LogWarning("[Shop] No ModuleGenerator - the shop cannot roll items");
            return;
        }

        foreach (var offer in offers)
        {
            offer.SetSold(false);
            offer.Item = generator.GenerateRandom(OfferLevel());
            Refresh(offer);
        }
    }

    /// <summary>
    /// Item level of one offer: it climbs with the run (<see cref="stopsPerLevel"/>), the node may
    /// demand a higher tier, and the variance adds a little spread.
    /// </summary>
    int OfferLevel()
    {
        var generator = ModuleGenerator.Instance;
        int max = generator != null ? generator.Config.MaxLevel : 3;

        int level = baseLevel + StopsTravelled() / Mathf.Max(1, stopsPerLevel);
        level = Mathf.Max(level, openLevel);

        int variance = Mathf.Max(0, levelVariance);
        if (variance > 0) level += Random.Range(-variance, variance + 1);

        return Mathf.Clamp(level, 1, Mathf.Max(1, max));
    }

    /// <summary>How far the run has come - the shop's level grows with it.</summary>
    static int StopsTravelled()
    {
        var cycle = G.Get<BattleCycle>();
        return cycle != null ? cycle.StopIndex : 0;
    }

    public int Price(ModuleInstance item)
    {
        if (item == null) return 0;

        return Mathf.Max(0, pricePerLevel) * Mathf.Max(1, item.Level);
    }

    /// <summary>Buys one offer - checked against the storage and the resource bank.</summary>
    public void Buy(ShopOfferUI offer)
    {
        if (offer == null || offer.Item == null || offer.Sold) return;

        G.Get<AudioManager>()?.PlaySfx("blipSelect");

        var inventory = G.Get<ModuleInventory>();
        if (inventory == null) return;

        // Storage first: nothing is spent when the item would not fit.
        if (!inventory.HasFreeSlot)
        {
            Debug.Log("[Shop] Storage is full - free a slot first");
            return;
        }

        var resources = G.Get<ResourceManager>();
        int cost = Price(offer.Item);

        if (resources != null && !resources.TrySpend(priceResource, cost))
        {
            Debug.Log($"[Shop] Not enough {priceResource}: {resources.Get(priceResource)}/{cost}");
            G.Get<AudioManager>()?.PlaySfx("error");
            return;
        }

        if (!inventory.TryAdd(offer.Item))
        {
            // Safety net: give the money back, the item stays on sale.
            resources?.Add(priceResource, cost);
            Debug.LogWarning("[Shop] Storage is full - purchase cancelled");
            return;
        }

        offer.SetSold(true);
        Refresh(offer);

        Debug.Log($"[Shop] Bought '{offer.Item.DisplayName}' for {cost} {priceResource}");
    }

    void Refresh(ShopOfferUI offer)
    {
        var item = offer.Item;

        if (offer.Icon != null)
        {
            offer.Icon.enabled = item != null;
            offer.Icon.sprite = item != null ? item.Icon : null;

            var color = offer.Icon.color;
            color.a = offer.Sold ? 0.35f : 1f;
            offer.Icon.color = color;
        }

        if (offer.Level != null)
        {
            offer.Level.text = item != null ? $"Lv.{item.Level}" : string.Empty;
        }

        // Price = resource icon + amount, sitting next to the slot.
        int cost = Price(item);
        var iconSprite = PriceIconSprite(priceResource);
        bool showPrice = item != null && !offer.Sold;

        if (offer.PriceIcon != null)
        {
            offer.PriceIcon.sprite = iconSprite;
            offer.PriceIcon.gameObject.SetActive(showPrice && iconSprite != null);
        }

        if (offer.Price != null)
        {
            offer.Price.text = offer.Sold
                ? "SOLD"
                : (showPrice ? cost.ToString() : string.Empty);
        }

        if (offer.Button != null) offer.Button.interactable = showPrice;
    }

    // ------------------------------------------------------------------ setup

    void EnsureRows()
    {
        if (offers.Count > 0) return;

        if (slotPrefab == null) slotPrefab = Resources.Load<ShopOfferUI>(SlotPrefabResourcePath);

        CachePriceIcons();
        CacheSlotSprite();

        var parent = offerContainer != null ? offerContainer : rect;
        float rowHeight = SlotHeight();
        float step = rowHeight + rowSpacing;
        float total = offerCount * rowHeight + Mathf.Max(0, offerCount - 1) * rowSpacing;
        float startY = total * 0.5f - rowHeight * 0.5f + offersOffsetY;

        for (int i = 0; i < offerCount; i++)
        {
            var slot = CreateSlot(parent, i);
            ((RectTransform)slot.transform).anchoredPosition =
                new Vector2(offersOffsetX, startY - i * step);

            offers.Add(slot);
        }
    }

    /// <summary>Height of one offer: the prefab dictates it when a prefab is used.</summary>
    float SlotHeight()
    {
        if (slotPrefab != null)
        {
            var prefabRect = (RectTransform)slotPrefab.transform;
            if (prefabRect.sizeDelta.y > 0f) return prefabRect.sizeDelta.y;
        }

        return Mathf.Max(1f, slotSize.y);
    }

    /// <summary>Instantiates the slot prefab (or builds an equivalent slot in code).</summary>
    ShopOfferUI CreateSlot(RectTransform parent, int index)
    {
        if (slotPrefab != null)
        {
            var instance = Instantiate(slotPrefab, parent, false);
            instance.name = $"Shop Offer {index}";

            var instanceRect = (RectTransform)instance.transform;
            instanceRect.anchorMin = new Vector2(0.5f, 0.5f);
            instanceRect.anchorMax = new Vector2(0.5f, 0.5f);
            instanceRect.pivot = new Vector2(0.5f, 0.5f);
            instanceRect.localScale = Vector3.one;

            if (instance.Button != null) instance.Button.onClick.AddListener(() => Buy(instance));

            return instance;
        }

        return CreateSlotInCode(parent, index);
    }

    /// <summary>Fallback slot used when no prefab is assigned: same look, built at runtime.</summary>
    ShopOfferUI CreateSlotInCode(RectTransform parent, int index)
    {
        var rowObject = new GameObject($"Shop Offer {index}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(ShopOfferUI));

        var row = (RectTransform)rowObject.transform;
        row.SetParent(parent, false);
        row.anchorMin = new Vector2(0.5f, 0.5f);
        row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.localScale = Vector3.one;
        row.sizeDelta = new Vector2(slotSize.x, slotSize.y);

        // Same background sprite and size as an inventory slot.
        var background = rowObject.GetComponent<Image>();
        background.sprite = slotSprite;
        background.color = Color.white;
        background.raycastTarget = true;

        var button = rowObject.GetComponent<Button>();
        button.targetGraphic = background;

        var offer = rowObject.GetComponent<ShopOfferUI>();

        // Icon fills the slot, exactly like the inventory does it.
        var icon = CreateImage(row, "Icon", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        var iconRect = (RectTransform)icon.transform;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        // Level badge in the bottom-right corner of the slot (same as the inventory).
        var level = CreateText(row, "Level",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-1.5f, 1.2f), new Vector2(20f, 9f), TextAlignmentOptions.BottomRight, 7f);

        // Price next to the slot: resource icon + amount.
        float priceX = slotSize.x * 0.5f + priceGap;

        var priceIcon = CreateImage(row, "PriceIcon",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(priceX, 0f), new Vector2(priceIconSize, priceIconSize));

        var price = CreateText(row, "Price",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(priceX + priceIconSize + 2f, 0f), new Vector2(24f, 10f),
            TextAlignmentOptions.MidlineLeft, 7f);

        price.color = priceColor;

        offer.Bind(icon, level, price, priceIcon, button);
        button.onClick.AddListener(() => Buy(offer));

        return offer;
    }

    void EnsureTitle()
    {
        if (title != null) return;

        title = CreateText((RectTransform)transform, "Title",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -4f), new Vector2(100f, 12f), TextAlignmentOptions.Top, 9f);

        title.fontStyle = FontStyles.Bold;
        title.text = "SHOP";
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Resource icons for the prices, taken from the HUD counters.</summary>
    void CachePriceIcons()
    {
        priceIcons.Clear();

        var counters = FindObjectsByType<ResourceCounterUI>(FindObjectsInactive.Include);
        foreach (var counter in counters)
        {
            if (counter == null || priceIcons.ContainsKey(counter.type)) continue;

            var iconTransform = counter.transform.Find("Icon");
            var image = iconTransform != null
                ? iconTransform.GetComponent<Image>()
                : counter.GetComponentInChildren<Image>(true);

            if (image != null && image.sprite != null) priceIcons[counter.type] = image.sprite;
        }

        if (priceIcons.Count == 0)
        {
            Debug.LogWarning("[Shop] No resource icons found - prices show numbers only");
        }
    }

    /// <summary>Reuses the inventory slot sprite so the shop slots look identical.</summary>
    void CacheSlotSprite()
    {
        if (slotSprite != null) return;

        var slots = FindObjectsByType<ModuleSlotUI>(FindObjectsInactive.Include);
        foreach (var slot in slots)
        {
            if (slot == null) continue;

            var image = slot.GetComponent<Image>();
            if (image == null || image.sprite == null) continue;

            slotSprite = image.sprite;
            return;
        }
    }

    Sprite PriceIconSprite(ResourceType type)
    {
        return priceIcons.TryGetValue(type, out var sprite) ? sprite : null;
    }

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

    Image CreateImage(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var imageRect = (RectTransform)go.transform;
        imageRect.SetParent(parent, false);
        imageRect.anchorMin = anchorMin;
        imageRect.anchorMax = anchorMax;
        imageRect.pivot = pivot;
        imageRect.anchoredPosition = anchoredPosition;
        imageRect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        return image;
    }
}
