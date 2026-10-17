using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The module panel shown after a victory. It is a passive view: it raises OnNext
/// and the BattleCycle decides what happens.
/// </summary>
public class ModulePanelUI : MonoBehaviour
{
    public Button nextButton;

    public event Action OnNext;

    /// <summary>Raised when the panel appears / disappears (used by world-space markers).</summary>
    public event Action Shown;
    public event Action Hidden;

    void Awake()
    {
        if (nextButton != null)
            nextButton.onClick.AddListener(HandleNext);
        else
            Debug.LogWarning("[ModulePanel] Next button is not assigned");
    }

    void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(HandleNext);
    }

    void HandleNext()
    {
        Hide();
        OnNext?.Invoke();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        Shown?.Invoke();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        Hidden?.Invoke();
    }
}
