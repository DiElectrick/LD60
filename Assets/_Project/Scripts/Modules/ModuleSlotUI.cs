using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One inventory slot on the module panel. Shows the module icon and starts drags.
/// </summary>
public class ModuleSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Index in ModuleInventory.")]
    public int slotIndex;

    [Tooltip("Image that displays the module icon.")]
    public Image icon;

    ModuleInventory inventory;
    ModuleDragController drag;

    void Start()
    {
        inventory = G.Get<ModuleInventory>();
        drag = FindAnyObjectByType<ModuleDragController>();

        if (inventory != null) inventory.Changed += Refresh;

        Refresh();
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.Changed -= Refresh;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        var data = inventory != null ? inventory.Get(slotIndex) : null;
        if (data == null || drag == null) return;

        drag.Begin(data, this, eventData);
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

    void Refresh()
    {
        if (icon == null) return;

        var data = inventory != null ? inventory.Get(slotIndex) : null;
        icon.sprite = data != null ? data.Icon : null;
        icon.enabled = data != null;
    }
}
