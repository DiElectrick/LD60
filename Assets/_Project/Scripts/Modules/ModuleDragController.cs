using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Module drag & drop: a ghost icon follows the pointer and the module is installed
/// into the platform column under the pointer. Stats are recalculated by CityController.
/// </summary>
public class ModuleDragController : MonoBehaviour
{
    [SerializeField] Canvas canvas;

    [Tooltip("How close to a platform column (world units on X) the drop must be.")]
    [SerializeField] float dropDistance = 1.4f;

    /// <summary>True while a module is being dragged (installation slots are shown then).</summary>
    public bool IsDragging { get; private set; }

    public event Action DragStarted;
    public event Action DragEnded;

    Image ghost;
    ModuleData dragging;
    ModuleSlotUI sourceSlot;
    Camera cam;
    RectTransform canvasRect;

    void Awake()
    {
        cam = Camera.main;
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) canvasRect = canvas.transform as RectTransform;
    }

    public void Begin(ModuleData data, ModuleSlotUI slot, PointerEventData eventData)
    {
        if (data == null) return;

        dragging = data;
        sourceSlot = slot;

        CreateGhost(data, slot);
        Move(eventData);

        IsDragging = true;
        DragStarted?.Invoke();
    }

    public void Move(PointerEventData eventData)
    {
        if (ghost == null || canvasRect == null) return;

        // The canvas renders through a camera, so screen -> canvas-local space,
        // otherwise the ghost would sit at a nonsense world position.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, eventData.position, canvas.worldCamera, out var localPoint))
        {
            ghost.rectTransform.anchoredPosition = localPoint;
        }
    }

    public void End(PointerEventData eventData)
    {
        DestroyGhost();

        var data = dragging;
        var slot = sourceSlot;
        dragging = null;
        sourceSlot = null;

        if (data != null) TryInstall(data, slot, eventData);

        IsDragging = false;
        DragEnded?.Invoke();
    }

    void TryInstall(ModuleData data, ModuleSlotUI slot, PointerEventData eventData)
    {
        var platform = FindPlatform(cam.ScreenToWorldPoint(eventData.position));
        if (platform == null)
        {
            Debug.Log("[Drag] Dropped outside the city - module stays in the inventory");
            return;
        }

        var city = G.Get<CityController>();
        if (city == null) return;

        // AddModule positions the module and recalculates the city stats.
        var module = city.AddModule(data, platform);
        if (module == null) return;

        slot?.ConsumeModule();
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

    void CreateGhost(ModuleData data, ModuleSlotUI slot)
    {
        if (canvas == null) return;

        var go = new GameObject("ModuleDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        ghost = go.GetComponent<Image>();
        ghost.sprite = data.Icon;
        ghost.preserveAspect = true;
        ghost.raycastTarget = false;

        var color = ghost.color;
        color.a = 0.7f;
        ghost.color = color;

        // Same size as the slot it was taken from.
        var rect = ghost.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        if (slot != null && slot.transform is RectTransform slotRect)
            rect.sizeDelta = slotRect.rect.size;
    }

    void DestroyGhost()
    {
        if (ghost == null) return;

        Destroy(ghost.gameObject);
        ghost = null;
    }
}
