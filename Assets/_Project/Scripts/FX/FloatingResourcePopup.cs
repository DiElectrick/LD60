using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Short "+N" that pops out of a HUD resource counter: the resource icon followed by the amount
/// gained. It drifts upwards from the counter and fades out, so income is read right where the
/// total lives - and with the same icon the counter uses - instead of as a number over the city.
/// <para>
/// The popup is built in code (no prefab) and copies the font, style and colour of the counter's
/// own label, so the HUD stays consistent whatever the counter looks like.
/// </para>
/// </summary>
public class FloatingResourcePopup : MonoBehaviour
{
    [Tooltip("How long the number lives.")]
    [SerializeField] float lifetime = 0.85f;

    [Tooltip("How far it drifts up over its lifetime, in canvas units.")]
    [SerializeField] float riseDistance = 18f;

    [Range(0f, 1f)]
    [Tooltip("Fraction of the lifetime after which the number starts to fade.")]
    [SerializeField] float fadeStart = 0.35f;

    [Tooltip("Random left/right offset at the start, so repeated gains never sit exactly on top.")]
    [SerializeField] float horizontalJitter = 6f;

    RectTransform rect;
    TMP_Text label;
    Color tint;

    Vector2 origin;
    float age;

    /// <summary>
    /// Builds and starts a gain popup as a child of <paramref name="anchor"/> (usually the
    /// resource counter itself) and returns it. <paramref name="style"/> supplies the look.
    /// </summary>
    public static FloatingResourcePopup Spawn(RectTransform anchor, Sprite iconSprite, int amount,
        TMP_Text style, Vector2 offset)
    {
        if (anchor == null || amount <= 0) return null;

        var root = new GameObject("ResourceGain", typeof(RectTransform));
        root.layer = anchor.gameObject.layer;

        var popupRect = (RectTransform)root.transform;
        popupRect.SetParent(anchor, false);
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.localScale = Vector3.one;

        var popup = root.AddComponent<FloatingResourcePopup>();
        popup.Build(iconSprite, amount, style);

        // Start slightly left or right of the counter so two gains in a row stay readable.
        popup.origin = offset + new Vector2(
            Random.Range(-popup.horizontalJitter, popup.horizontalJitter), 0f);
        popupRect.anchoredPosition = popup.origin;

        return popup;
    }

    void Build(Sprite iconSprite, int amount, TMP_Text style)
    {
        rect = (RectTransform)transform;

        float fontSize = style != null && style.fontSize > 0f ? style.fontSize : 12f;
        float iconSize = Mathf.Max(8f, fontSize * 1.2f);
        float gap = Mathf.Max(1f, fontSize * 0.3f);

        tint = style != null ? style.color : Color.white;

        // The amount: measured first, so icon + number stay centred under the counter whatever
        // the size of the gain.
        var textObject = new GameObject("Amount", typeof(RectTransform));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(rect, false);

        var textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(0f, 0.5f);
        textRect.pivot = new Vector2(0f, 0.5f);

        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = style != null && style.font != null ? style.font : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style != null ? style.fontStyle : FontStyles.Normal;
        text.color = tint;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.richText = false;
        text.text = $"+{Mathf.Abs(amount)}";

        float textWidth = Mathf.Max(fontSize, text.GetPreferredValues(text.text).x);
        float width = iconSize + gap + textWidth;

        rect.sizeDelta = new Vector2(width, iconSize);

        textRect.anchoredPosition = new Vector2(iconSize + gap, 0f);
        textRect.sizeDelta = new Vector2(textWidth, iconSize);

        // The resource icon, the same art the counter shows, on the left of the amount.
        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.layer = gameObject.layer;
        iconObject.transform.SetParent(rect, false);

        var iconRect = (RectTransform)iconObject.transform;
        iconRect.SetAsFirstSibling();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);

        var icon = iconObject.GetComponent<Image>();
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = iconSprite != null;

        label = text;
    }

    void Update()
    {
        age += Time.deltaTime;

        float progress = Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));

        // Ease-out: it leaves quickly, then settles while fading.
        float eased = 1f - (1f - progress) * (1f - progress);
        rect.anchoredPosition = origin + new Vector2(0f, riseDistance * eased);

        if (label != null)
        {
            var color = tint;
            color.a = 1f - Mathf.Clamp01((progress - fadeStart) / Mathf.Max(0.01f, 1f - fadeStart));
            label.color = color;
        }

        if (age >= lifetime) Destroy(gameObject);
    }
}
