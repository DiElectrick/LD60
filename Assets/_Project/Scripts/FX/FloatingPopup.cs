using TMPro;
using UnityEngine;

/// <summary>What a floating popup reports.</summary>
public enum PopupKind
{
    /// <summary>"-N" in red - a unit was hurt. Launched up, then falls under gravity.</summary>
    Damage,

    /// <summary>"+N" in green - a unit was repaired. Same arc as damage, healing colours.</summary>
    Heal,
}

/// <summary>
/// Floating world-space number above a unit: "-12" when it is hurt, "+8" when it is repaired.
/// The number is launched upwards, falls under gravity and fades out.
/// <para>
/// Resource gains are a HUD matter and pop out of their counter instead
/// (see <see cref="FloatingResourcePopup"/>).
/// </para>
/// </summary>
public class FloatingPopup : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    [Tooltip("How long the number lives.")]
    [SerializeField] float lifetime = 0.9f;

    [SerializeField] float gravity = -16f;

    [Tooltip("Random left/right launch speed.")]
    [SerializeField] float horizontalSpread = 2.5f;

    [Tooltip("Upward launch speed.")]
    [SerializeField] float upwardSpeed = 6f;

    [Range(0f, 1f)]
    [Tooltip("Fraction of the lifetime after which the number starts to fade.")]
    [SerializeField] float fadeStart = 0.55f;

    [Header("Colours")]
    [SerializeField] Color damageColor = new(1f, 0.35f, 0.3f, 1f);
    [SerializeField] Color healColor = new(0.4f, 1f, 0.45f, 1f);

    Color tint;
    Vector3 velocity;
    float age;
    float duration;

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);

        tint = damageColor;
        duration = lifetime;
    }

    /// <summary>Fills the number and starts its motion. Called right after instantiation.</summary>
    public void Show(int amount, PopupKind popupKind = PopupKind.Damage)
    {
        duration = Mathf.Max(0.01f, lifetime);
        tint = popupKind == PopupKind.Heal ? healColor : damageColor;

        age = 0f;

        if (label != null)
        {
            label.text = (popupKind == PopupKind.Damage ? "-" : "+") + Mathf.Abs(amount);
            label.color = tint;
        }

        velocity = new Vector3(
            Random.Range(-horizontalSpread, horizontalSpread),
            upwardSpeed,
            0f);
    }

    void Update()
    {
        age += Time.deltaTime;

        // Launched upwards, then pulled down by gravity.
        velocity.y += gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;

        Fade();

        if (age >= duration) Destroy(gameObject);
    }

    void Fade()
    {
        if (label == null) return;

        float start = Mathf.Clamp01(fadeStart);
        float alpha = 1f - Mathf.Clamp01((age / duration - start) / Mathf.Max(0.01f, 1f - start));

        var faded = tint;
        faded.a = alpha;
        label.color = faded;
    }
}
