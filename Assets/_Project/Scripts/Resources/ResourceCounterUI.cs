using TMPro;
using UnityEngine;

/// <summary>
/// Displays the amount of one resource. Put it on a counter object that has a
/// TMP text child (usually called "CountText").
/// </summary>
public class ResourceCounterUI : MonoBehaviour
{
    public ResourceType type = ResourceType.ScrapMetal;

    public TMP_Text label;

    ResourceManager manager;

    void Start()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);

        manager = G.Get<ResourceManager>();
        if (manager == null)
        {
            Debug.LogWarning("[ResourceCounter] ResourceManager is not registered");
            return;
        }

        manager.Changed += OnChanged;
        Refresh();
    }

    void OnDestroy()
    {
        if (manager != null) manager.Changed -= OnChanged;
    }

    void OnChanged(ResourceType changedType, int total)
    {
        if (changedType == type) Refresh();
    }

    void Refresh()
    {
        if (label != null) label.text = manager.Get(type).ToString();
    }
}
