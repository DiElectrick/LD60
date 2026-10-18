using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One repair option of the repair station.
/// <para>
/// The button lives in the scene, so its position, size, colours and texts are adjusted by
/// hand. It only carries the data of the action (how much durability it restores, what it
/// costs) and forwards its click to the panel. Add another option by duplicating the object
/// and editing its numbers.
/// </para>
/// </summary>
public class RepairButtonUI : MonoBehaviour
{
    [Header("Action")]
    [Tooltip("Durability (city health) restored by this button.")]
    [Min(0)]
    public int amount = 25;

    [Tooltip("Cost; 0 = free.")]
    [Min(0)]
    public int cost = 0;

    [Tooltip("Resource the cost is paid with.")]
    public ResourceType resource = ResourceType.ScrapMetal;

    [Header("View")]
    [Tooltip("Clickable button. Empty = the Button on this object.")]
    [SerializeField] Button button;

    [Tooltip("Label of the button. Empty = the first TMP text below this object.")]
    [SerializeField] TMP_Text title;

    [Tooltip("Price label. Empty = the second TMP text below this object.")]
    [SerializeField] TMP_Text price;

    public Button Button => button;
    public TMP_Text Title => title;
    public TMP_Text Price => price;

    RepairPanelUI panel;

    /// <summary>Wires the button to its panel (called by the panel when it opens).</summary>
    public void Bind(RepairPanelUI owner)
    {
        panel = owner;
        ResolveReferences();

        if (button == null) return;

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    /// <summary>Fills in the references that were left empty in the inspector.</summary>
    public void ResolveReferences()
    {
        if (button == null) button = GetComponent<Button>();

        if (title != null && price != null) return;

        var texts = GetComponentsInChildren<TMP_Text>(true);
        if (title == null && texts.Length > 0) title = texts[0];
        if (price == null && texts.Length > 1) price = texts[1];
    }

    void HandleClick()
    {
        if (panel != null) panel.Run(this);
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }
}
