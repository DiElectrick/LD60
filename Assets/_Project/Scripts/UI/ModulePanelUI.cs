using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The module panel shown after a victory. It slides in from below the screen and
/// is otherwise a passive view: it raises OnNext and the BattleCycle decides what happens.
/// </summary>
public class ModulePanelUI : MonoBehaviour
{
    public Button nextButton;

    [SerializeField] float slideDuration = 0.45f;
    [SerializeField] float slidePadding = 40f;

    public event Action OnNext;

    /// <summary>Raised when the panel appears / disappears (used by world-space markers).</summary>
    public event Action Shown;
    public event Action Hidden;

    RectTransform rect;
    Vector2 visiblePosition;
    bool positionCaptured;

    void Awake()
    {
        rect = (RectTransform)transform;

        if (nextButton != null)
            nextButton.onClick.AddListener(HandleNext);
        else
            Debug.LogWarning("[ModulePanel] Next button is not assigned");
    }

    void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(HandleNext);

        if (rect != null) rect.DOKill();
    }

    void HandleNext()
    {
        Hide();
        OnNext?.Invoke();
    }

    /// <summary>Shows the panel by sliding it in from below the screen edge.</summary>
    public void Show()
    {
        gameObject.SetActive(true);

        if (rect == null) rect = (RectTransform)transform;

        if (!positionCaptured)
        {
            visiblePosition = rect.anchoredPosition;
            positionCaptured = true;
        }

        float distance = rect.rect.height + slidePadding;

        rect.DOKill();
        rect.anchoredPosition = visiblePosition + new Vector2(0f, -distance);
        rect.DOAnchorPos(visiblePosition, slideDuration).SetEase(Ease.OutCubic);

        Shown?.Invoke();
    }

    /// <summary>Hides the panel by sliding it back behind the screen edge.</summary>
    public void Hide()
    {
        Hidden?.Invoke();

        if (rect == null || !gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!positionCaptured)
        {
            visiblePosition = rect.anchoredPosition;
            positionCaptured = true;
        }

        float distance = rect.rect.height + slidePadding;

        rect.DOKill();
        rect.DOAnchorPos(visiblePosition + new Vector2(0f, -distance), slideDuration)
            .SetEase(Ease.InCubic)
            .OnComplete(() => gameObject.SetActive(false));
    }
}
