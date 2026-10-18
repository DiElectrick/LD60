using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the amount of one resource. Put it on a counter object that has a
/// TMP text child (usually called "CountText").
/// <para>
/// The counter also reports every gain: a small "+N" pops out of it with the resource icon
/// (see <see cref="FloatingResourcePopup"/>), so harvesting, loot and recycling are all visible
/// right where the total is.
/// </para>
/// </summary>
public class ResourceCounterUI : MonoBehaviour
{
    public ResourceType type = ResourceType.ScrapMetal;

    public TMP_Text label;

    [Header("Gain popup")]
    [Tooltip("Icon shown next to the \"+N\" popup when this resource is gained. Empty = the counter's own icon.")]
    [SerializeField] Sprite gainIcon;

    [Tooltip("Where the popup starts, relative to the centre of the counter (negative Y = below it).")]
    [SerializeField] Vector2 gainPopupOffset = new(0f, -20f);

    ResourceManager manager;
    Image iconImage;
    int lastAmount;

    void Start()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        iconImage = FindIcon();

        manager = G.Get<ResourceManager>();
        if (manager == null)
        {
            Debug.LogWarning("[ResourceCounter] ResourceManager is not registered");
            return;
        }

        lastAmount = manager.Get(type);
        manager.Changed += OnChanged;
        Refresh();
    }

    void OnDestroy()
    {
        if (manager != null) manager.Changed -= OnChanged;
    }

    void OnChanged(ResourceType changedType, int total)
    {
        if (changedType != type) return;

        int gained = total - lastAmount;
        lastAmount = total;

        Refresh();

        // Spending is already readable from the counter itself - only gains pop up.
        if (gained > 0) ShowGain(gained);
    }

    void Refresh()
    {
        if (label != null) label.text = manager.Get(type).ToString();
    }

    // ------------------------------------------------------------------ gain popup

    void ShowGain(int amount)
    {
        FloatingResourcePopup.Spawn((RectTransform)transform, GainIconSprite(), amount, label, gainPopupOffset);
    }

    Sprite GainIconSprite()
    {
        if (gainIcon != null) return gainIcon;

        return iconImage != null ? iconImage.sprite : null;
    }

    Image FindIcon()
    {
        var iconTransform = transform.Find("Icon");
        if (iconTransform != null)
        {
            var named = iconTransform.GetComponent<Image>();
            if (named != null) return named;
        }

        return GetComponentInChildren<Image>(true);
    }
}
