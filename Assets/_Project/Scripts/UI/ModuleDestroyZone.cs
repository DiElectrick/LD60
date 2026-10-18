using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marks the module panel's destruction zone. Dropping a module here scraps it into
/// resources (see <see cref="ModuleRecycling"/>); the zone tints itself while a module
/// is carried over it.
/// </summary>
public class ModuleDestroyZone : MonoBehaviour
{
    [Tooltip("Image tinted while a module hovers over the zone. Found automatically.")]
    [SerializeField] Image background;

    [Tooltip("Tint shown while a module can be dropped here.")]
    [SerializeField] Color highlightColor = new(1f, 0.42f, 0.32f, 1f);

    Color baseColor;
    bool captured;

    public RectTransform Rect => (RectTransform)transform;

    void Awake()
    {
        if (background == null) background = GetComponent<Image>();
        if (background == null) background = GetComponentInChildren<Image>(true);

        Capture();
    }

    /// <summary>Tints the zone while a dragged module hovers over it.</summary>
    public void SetHighlight(bool highlighted)
    {
        if (background == null) return;

        Capture();

        background.color = highlighted ? highlightColor : baseColor;
    }

    void Capture()
    {
        if (captured || background == null) return;

        baseColor = background.color;
        captured = true;
    }
}
