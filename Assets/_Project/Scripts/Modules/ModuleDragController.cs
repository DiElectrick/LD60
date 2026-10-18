using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Module drag &amp; drop in both directions:
/// <list type="bullet">
/// <item>inventory slot -&gt; city: the ghost follows the pointer and the item is installed
/// into the platform column under the pointer;</item>
/// <item>city -&gt; storage (build mode): an installed module can be picked up and dropped
/// into an inventory slot. The module only leaves the city when the drop succeeds.</item>
/// </list>
/// Stats are recalculated by CityController, so an item keeps its level and rolls wherever
/// it is placed.
/// </summary>
public class ModuleDragController : MonoBehaviour
{
    public enum DragOrigin
    {
        Slot,
        City,
    }

    [SerializeField] Canvas canvas;

    [Tooltip("How close to a platform column (world units on X) the drop must be.")]
    [SerializeField] float dropDistance = 1.4f;

    [Tooltip("Ghost size used when the drag started in the city (no inventory slot to copy).")]
    [SerializeField] Vector2 cityGhostSize = new(40f, 25f);

    [Tooltip("Alpha of the module left behind in the city while it is being carried.")]
    [Range(0f, 1f)]
    [SerializeField] float carriedAlpha = 0.35f;

    [Tooltip("Tint of an inventory slot the carried module can be dropped into.")]
    [SerializeField] Color dropHighlightColor = new(0.7f, 1f, 0.7f, 1f);

    /// <summary>True while a module is being dragged (installation slots are shown then).</summary>
    public bool IsDragging { get; private set; }

    /// <summary>True when the module comes from the city and is being carried to the storage.</summary>
    public bool IsDraggingFromCity => IsDragging && origin == DragOrigin.City;

    public event Action DragStarted;
    public event Action DragEnded;

    /// <summary>Raised when an item was taken out of the storage and installed in the city.</summary>
    public event Action<ModuleInstance> ModuleInstalled;

    /// <summary>What the pointer is over while dragging: an inventory slot or the destruction zone.</summary>
    readonly struct DropTarget
    {
        public readonly ModuleSlotUI Slot;
        public readonly ModuleDestroyZone Zone;

        public DropTarget(ModuleSlotUI slot, ModuleDestroyZone zone)
        {
            Slot = slot;
            Zone = zone;
        }
    }

    Image ghost;
    ModuleInstance dragging;
    ModuleSlotUI sourceSlot;
    ModuleSlotUI highlightedSlot;
    ModuleDestroyZone highlightedZone;

    DragOrigin origin;
    Module cityModule;
    Color cityModuleColor;

    Camera cam;
    RectTransform canvasRect;

    void Awake()
    {
        cam = Camera.main;
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) canvasRect = canvas.transform as RectTransform;
    }

    void Update()
    {
        // A city drag has no UI event source - the controller polls the pointer itself.
        if (!IsDraggingFromCity) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 screenPosition = mouse.position.ReadValue();
        Move(screenPosition);

        if (mouse.leftButton.wasReleasedThisFrame) End(screenPosition);
    }

    // ------------------------------------------------------------------ begin

    /// <summary>Starts dragging an inventory item towards the city.</summary>
    public void Begin(ModuleInstance item, ModuleSlotUI slot, PointerEventData eventData)
    {
        if (item == null || IsDragging) return;

        origin = DragOrigin.Slot;
        dragging = item;
        sourceSlot = slot;
        cityModule = null;

        CreateGhost(item, slot);
        Move(eventData != null ? eventData.position : Vector2.zero);

        IsDragging = true;
        DragStarted?.Invoke();
    }

    /// <summary>Starts carrying an installed module to the storage (build mode).</summary>
    public void BeginFromCity(Module module)
    {
        if (module == null || module.Instance == null || IsDragging) return;

        origin = DragOrigin.City;
        dragging = module.Instance;
        sourceSlot = null;
        cityModule = module;

        // Dim the module in the city so it reads as "picked up".
        var view = module.View;
        if (view != null)
        {
            cityModuleColor = view.color;

            var carried = cityModuleColor;
            carried.a *= carriedAlpha;
            view.color = carried;
        }

        CreateGhost(dragging, null);
        Move(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);

        IsDragging = true;
        DragStarted?.Invoke();
    }

    // ------------------------------------------------------------------ move

    public void Move(PointerEventData eventData)
    {
        if (eventData != null) Move(eventData.position);
    }

    public void Move(Vector2 screenPosition)
    {
        if (ghost != null && canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, canvas.worldCamera, out var localPoint))
        {
            ghost.rectTransform.anchoredPosition = localPoint;
        }

        HighlightDropTarget(screenPosition);
    }

    // ------------------------------------------------------------------ end

    public void End(PointerEventData eventData)
    {
        End(eventData != null ? eventData.position : Vector2.zero);
    }

    public void End(Vector2 screenPosition)
    {
        if (!IsDragging) return;

        DestroyGhost();
        SetSlotHighlight(null);
        SetZoneHighlight(null);

        var item = dragging;
        var slot = sourceSlot;
        var module = cityModule;
        var fromCity = origin == DragOrigin.City;

        dragging = null;
        sourceSlot = null;
        cityModule = null;
        origin = DragOrigin.Slot;
        IsDragging = false;

        if (item != null)
        {
            if (fromCity) DropFromCity(module, item, screenPosition);
            else DropFromSlot(item, slot, screenPosition);
        }

        DragEnded?.Invoke();
    }

    // ------------------------------------------------------------------ drops

    void DropFromSlot(ModuleInstance item, ModuleSlotUI slot, Vector2 screenPosition)
    {
        var target = ResolveDropTarget(screenPosition);

        // Dropped on the destruction zone: scrap the item straight out of the storage.
        if (target.Zone != null)
        {
            RecycleFromStorage(item, slot);
            return;
        }

        if (cam == null) return;

        var platform = FindPlatform(cam.ScreenToWorldPoint(ToWorldPoint(screenPosition)));
        if (platform == null)
        {
            DevLog.Info("[Drag] Dropped outside the city - module stays in the inventory");
            return;
        }

        var city = G.Get<CityController>();
        if (city == null) return;

        // AddModule positions the module and recalculates the city stats.
        var module = city.AddModule(item, platform);
        if (module == null) return;

        G.Get<AudioManager>()?.PlaySfx("set");

        slot?.ConsumeModule();

        ModuleInstalled?.Invoke(item);
    }

    void DropFromCity(Module module, ModuleInstance item, Vector2 screenPosition)
    {
        var target = ResolveDropTarget(screenPosition);

        // Dropped on the destruction zone: the city gives the module up and it is scrapped.
        if (target.Zone != null)
        {
            RecycleFromCity(module, item);
            return;
        }

        // Otherwise the storage is the inventory grid, so the drop has to land on a slot.
        if (target.Slot == null)
        {
            DevLog.Info("[Drag] Released outside the storage - module stays in the city");
            CancelCityDrag(module);
            return;
        }

        var inventory = G.Get<ModuleInventory>();
        var city = G.Get<CityController>();

        if (inventory == null || city == null || module == null)
        {
            CancelCityDrag(module);
            return;
        }

        string moduleName = module.name;

        // The city drops the module first; if the storage refuses it, it goes back.
        if (!city.RemoveModule(module))
        {
            CancelCityDrag(module);
            return;
        }

        bool stored = inventory.TryAdd(item, target.Slot.slotIndex);
        if (!stored) stored = inventory.TryAdd(item);

        if (stored)
        {
            DevLog.Info($"[Drag] Moved '{item.DisplayName}' from {moduleName} to the storage");
            return;
        }

        Debug.LogWarning($"[Drag] Storage is full - '{item.DisplayName}' goes back to the city");
        city.AddModule(item);
    }

    // ------------------------------------------------------------------ recycling

    /// <summary>Scraps an installed module: it leaves the city and yields its resources.</summary>
    void RecycleFromCity(Module module, ModuleInstance item)
    {
        var city = G.Get<CityController>();
        if (city == null || module == null)
        {
            CancelCityDrag(module);
            return;
        }

        // Check the recipe before removing anything - an unrecyclable module stays put.
        var drops = ModuleRecycling.Resolve(item);
        if (drops.Count == 0)
        {
            Debug.LogWarning($"[Recycle] '{item.DisplayName}' has no recycling recipe - module stays in the city");
            CancelCityDrag(module);
            return;
        }

        if (!city.RemoveModule(module))
        {
            CancelCityDrag(module);
            return;
        }

        Grant(item, drops);
    }

    /// <summary>Scraps an item that is still sitting in the storage.</summary>
    void RecycleFromStorage(ModuleInstance item, ModuleSlotUI slot)
    {
        var drops = ModuleRecycling.Resolve(item);
        if (drops.Count == 0)
        {
            Debug.LogWarning($"[Recycle] '{item.DisplayName}' has no recycling recipe - item stays in the storage");
            return;
        }

        slot?.ConsumeModule();
        Grant(item, drops);
    }

    void Grant(ModuleInstance item, List<ResourceDrop> drops)
    {
        // Scrapping has its own feedback: the module is torn apart, not placed in the city.
        G.Get<AudioManager>()?.PlaySfx("set");

        G.Get<ResourceManager>()?.AddDrops(drops);

        DevLog.Info($"[Recycle] Scrapped '{item.DisplayName}' -> {ModuleRecycling.Describe(drops)}");
    }

    void CancelCityDrag(Module module)
    {
        // The module never left the city - just restore the look we dimmed it with.
        RestoreCityModule(module);
    }

    void RestoreCityModule(Module module)
    {
        if (module == null) return;

        var view = module.View;
        if (view == null) return;

        var color = view.color;
        color.a = cityModuleColor.a;
        view.color = color;
    }

    // ------------------------------------------------------------------ targets

    /// <summary>What is under the pointer: an inventory slot, the destruction zone or nothing.</summary>
    DropTarget ResolveDropTarget(Vector2 screenPosition)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return default;

        var pointer = new PointerEventData(eventSystem) { position = screenPosition };
        var hits = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, hits);

        ModuleSlotUI slot = null;
        ModuleDestroyZone zone = null;

        foreach (var hit in hits)
        {
            if (hit.gameObject == null) continue;

            if (slot == null) slot = hit.gameObject.GetComponentInParent<ModuleSlotUI>();
            if (zone == null) zone = hit.gameObject.GetComponentInParent<ModuleDestroyZone>();

            if (slot != null && zone != null) break;
        }

        return new DropTarget(slot, zone);
    }

    Platform FindPlatform(Vector3 worldPoint)
    {
        var city = G.Get<CityController>();
        if (city == null) return null;

        Platform best = null;
        float bestDistance = dropDistance;

        foreach (var platform in city.Platforms)
        {
            if (platform.IsFull) continue;

            float dx = Mathf.Abs(platform.transform.position.x - worldPoint.x);
            if (dx >= bestDistance) continue;

            best = platform;
            bestDistance = dx;
        }

        return best;
    }

    /// <summary>Screen point with the depth of the game plane (2D camera).</summary>
    static Vector3 ToWorldPoint(Vector2 screenPosition)
    {
        return new Vector3(screenPosition.x, screenPosition.y, 0f);
    }

    // ------------------------------------------------------------------ highlight

    void HighlightDropTarget(Vector2 screenPosition)
    {
        var target = ResolveDropTarget(screenPosition);

        SetSlotHighlight(target.Slot);
        SetZoneHighlight(target.Zone);
    }

    void SetSlotHighlight(ModuleSlotUI slot)
    {
        if (highlightedSlot == slot) return;

        if (highlightedSlot != null) highlightedSlot.SetDropHighlight(false);

        highlightedSlot = slot;

        if (highlightedSlot != null) highlightedSlot.SetDropHighlight(true, dropHighlightColor);
    }

    void SetZoneHighlight(ModuleDestroyZone zone)
    {
        if (highlightedZone == zone) return;

        if (highlightedZone != null) highlightedZone.SetHighlight(false);

        highlightedZone = zone;

        if (highlightedZone != null) highlightedZone.SetHighlight(true);
    }

    // ------------------------------------------------------------------ ghost

    void CreateGhost(ModuleInstance item, ModuleSlotUI slot)
    {
        if (canvas == null) return;

        var go = new GameObject("ModuleDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        ghost = go.GetComponent<Image>();
        ghost.sprite = item.Icon;
        ghost.preserveAspect = true;
        ghost.raycastTarget = false;

        var color = ghost.color;
        color.a = 0.7f;
        ghost.color = color;

        var rect = ghost.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        // Same size as the slot it was taken from, or the module size for city drags.
        rect.sizeDelta = slot != null && slot.transform is RectTransform slotRect
            ? slotRect.rect.size
            : cityGhostSize;
    }

    void DestroyGhost()
    {
        if (ghost == null) return;

        Destroy(ghost.gameObject);
        ghost = null;
    }
}
