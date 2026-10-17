using TMPro;
using UnityEngine;

/// <summary>
/// Floating "-N" damage number: launches upwards with some horizontal spread,
/// falls under gravity and fades out.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    [SerializeField] TMP_Text label;

    [SerializeField] float lifetime = 0.9f;
    [SerializeField] float gravity = -16f;

    [Tooltip("Random left/right launch speed.")]
    [SerializeField] float horizontalSpread = 2.5f;

    [Tooltip("Upward launch speed.")]
    [SerializeField] float upwardSpeed = 6f;

    [Range(0f, 1f)]
    [Tooltip("Fraction of the lifetime after which the number starts to fade.")]
    [SerializeField] float fadeStart = 0.55f;

    Vector3 velocity;
    float age;
    Color baseColor;

    void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label != null) baseColor = label.color;
    }

    public void Show(int amount)
    {
        if (label != null)
        {
            label.text = $"-{Mathf.Abs(amount)}";
            label.color = baseColor;
        }

        velocity = new Vector3(
            Random.Range(-horizontalSpread, horizontalSpread),
            upwardSpeed,
            0f);
    }

    void Update()
    {
        age += Time.deltaTime;

        velocity.y += gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;

        if (label != null)
        {
            var color = baseColor;
            color.a = 1f - Mathf.Clamp01((age / lifetime - fadeStart) / (1f - fadeStart));
            label.color = color;
        }

        if (age >= lifetime) Destroy(gameObject);
    }
}
