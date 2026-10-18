using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The travel between two stops of the run.
/// <para>
/// The city gets back up to speed and drives for a moment, then a foreground object slides in
/// front of it (it is drawn above every sprite, below the canvas) at a speed just above the
/// parallax ground, so the city seems to keep moving behind it. Once it is in place the route
/// bar appears on top of it and fills from the node the city just left to the node it is
/// heading to; finally the foreground slides away to reveal the next battle.
/// </para>
/// Everything is authored in the scene: the foreground object is placed where it sits when it
/// covers the screen, and the bar and its icons are canvas children - so the whole transition
/// can be art-directed without touching code.
/// <para>
/// The end of the run re-uses the same transition: the bar stays full and the end message
/// with the restart button appears on top of it.
/// </para>
/// </summary>
public class TravelTransition : MonoBehaviour
{
    [Header("Foreground")]
    [Tooltip("The object that slides in front of the city. Place it in the scene where it " +
             "should sit when it covers the screen. Keep its sorting order above the world " +
             "sprites but below the HUD canvas (the canvas sorts at 552 in this scene).")]
    [SerializeField] Transform foreground;

    [Tooltip("Where the foreground waits while it is not travelling (offset from its scene position).")]
    [SerializeField] Vector3 hiddenOffset = new(22f, 0f, 0f);

    [Header("Entry")]
    [Tooltip("How long the city gets up to speed and drives before the foreground starts " +
             "moving in. 0 = the foreground enters right after Next.")]
    [SerializeField] float driveBeforeEntry = 4f;

    [Tooltip("Ground layer the entry speed is matched to. Empty = the first GroundParallaxLayer " +
             "in the scene.")]
    [SerializeField] GroundParallaxLayer groundLayer;

    [Tooltip("Entry speed relative to the ground layer: 1 = exactly the ground speed, " +
             "a little above 1 makes the foreground slowly overtake the city.")]
    [SerializeField] float entrySpeedFactor = 1.1f;

    [Tooltip("Entry time used when there is no ground layer to match.")]
    [SerializeField] float fallbackEntryDuration = 0.5f;

    [Tooltip("How long the world takes to get back up to speed when the travel starts.")]
    [SerializeField] float rampDuration = 0.4f;

    [Tooltip("Exit time used when there is no ground layer to match. The foreground normally " +
             "leaves at the same speed it came in.")]
    [SerializeField] float fallbackExitDuration = 0.5f;

    [Tooltip("Small pause after the foreground has left before the battle starts.")]
    [SerializeField] float battleStartDelay = 0.3f;

    [Header("Route bar")]
    [Tooltip("Root of the transition UI: the bar with its node icons. Empty = nothing is shown.")]
    [SerializeField] RectTransform screen;

    [Tooltip("Canvas group that fades the transition UI. Empty = taken from the screen root.")]
    [SerializeField] CanvasGroup screenGroup;

    [Tooltip("Image that shows the travelled part of the route. Use Image type 'Filled'.")]
    [SerializeField] Image barFill;

    [SerializeField] float fadeDuration = 0.2f;
    [SerializeField] float fillDuration = 0.9f;
    [SerializeField] float holdDuration = 0.7f;

    [Header("End of run")]
    [Tooltip("Message + restart button shown when the boss is dead. Empty = nothing is shown.")]
    [SerializeField] RectTransform endInfo;

    [Tooltip("Restart button of the end screen. Empty = found by name below the end info.")]
    [SerializeField] Button restartButton;

    Vector3 coveredPosition;
    CanvasGroup endGroup;
    ParallaxSystem parallax;
    Action restartAction;
    bool isPlaying;
    bool isEndShown;

    /// <summary>True while a travel (or the end screen) is taking over the screen.</summary>
    public bool IsPlaying => isPlaying;

    /// <summary>True once the run is over and the end message is on screen.</summary>
    public bool IsEndShown => isEndShown;

    /// <summary>False while another travel is still running - ignore further transitions.</summary>
    public bool CanPlay => !isPlaying && !isEndShown;

    /// <summary>How long the world takes to get back up to speed when a travel starts.</summary>
    public float RampDuration => rampDuration;

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        if (foreground != null)
        {
            // Authored where it covers the screen; it only shows up while travelling.
            coveredPosition = foreground.position;
            foreground.gameObject.SetActive(false);
        }

        parallax = FindAnyObjectByType<ParallaxSystem>();

        if (groundLayer == null)
            groundLayer = FindAnyObjectByType<GroundParallaxLayer>(FindObjectsInactive.Include);

        if (screen != null && screenGroup == null)
            screenGroup = screen.GetComponent<CanvasGroup>();

        if (endInfo != null)
        {
            endGroup = endInfo.GetComponent<CanvasGroup>();
            endInfo.gameObject.SetActive(false);
        }

        HideScreen();

        if (restartButton == null && endInfo != null)
            restartButton = endInfo.GetComponentInChildren<Button>(true);

        if (restartButton != null) restartButton.onClick.AddListener(HandleRestart);
    }

    void OnDestroy()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(HandleRestart);
    }

    // ------------------------------------------------------------------ travel

    /// <summary>
    /// Plays one travel: the foreground covers the screen, the bar fills from
    /// <paramref name="from"/> to <paramref name="to"/> and <paramref name="onArrived"/>
    /// runs once the next fight can start.
    /// </summary>
    public void Play(float from, float to, Action onArrived)
    {
        if (!CanPlay) return;

        isPlaying = true;
        StopAllCoroutines();

        Debug.Log($"[Travel] Covering the world - route bar {from:F2} -> {to:F2}");
        StartCoroutine(TravelRoutine(from, to, onArrived, false));
    }

    /// <summary>
    /// Plays the same travel for the end of the run: the bar is shown (already full) and the
    /// end message with the restart button stays up until <paramref name="restart"/> is used.
    /// </summary>
    public void PlayEnd(float fill, Action restart)
    {
        if (!CanPlay) return;

        restartAction = restart;
        isPlaying = true;
        StopAllCoroutines();

        Debug.Log("[Travel] End of the run - showing the complete route");
        StartCoroutine(TravelRoutine(fill, fill, null, true));
    }

    IEnumerator TravelRoutine(float from, float to, Action onArrived, bool end)
    {
        PrepareForTravel(from);

        // 1. the city gets up to speed and simply drives for a moment before anything moves in
        if (!end && driveBeforeEntry > 0f)
        {
            Debug.Log($"[Travel] City driving for {driveBeforeEntry:F1}s before the foreground enters");
            yield return new WaitForSeconds(driveBeforeEntry);
        }

        // 2. the foreground slides in at a speed a little above the parallax ground, then holds
        if (foreground != null)
        {
            float entryDuration = EntryDuration();
            float entrySpeed = EntrySpeed();

            Debug.Log($"[Travel] Foreground entering in {entryDuration:F2}s at {entrySpeed:F2} units/s");

            foreground.gameObject.SetActive(true);
            foreground.position = coveredPosition + hiddenOffset;

            yield return foreground.DOMove(coveredPosition, entryDuration)
                .SetEase(Ease.Linear)
                .WaitForCompletion();
        }

        // 3. the route bar appears against the foreground and fills up to the next node
        if (screenGroup != null)
        {
            yield return screenGroup.DOFade(1f, fadeDuration).WaitForCompletion();

            Debug.Log($"[Travel] Route bar shown (alpha {screenGroup.alpha:F2})");
        }

        if (barFill != null)
        {
            float target = Mathf.Clamp01(to);

            // The end screen shows a bar that is already full - no need to animate it.
            if (Mathf.Abs(target - barFill.fillAmount) > 0.001f)
            {
                yield return barFill.DOFillAmount(target, fillDuration)
                    .SetEase(Ease.InOutQuad)
                    .WaitForCompletion();
            }
            else
            {
                barFill.fillAmount = target;
            }
        }

        if (end)
        {
            // The bar is done - keep the screen up and show the end message + restart.
            ShowEndInfo();

            isPlaying = false;
            isEndShown = true;
            yield break;
        }

        // 4. the bar is done - the foreground carries on to the left, at the speed it came in
        if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

        if (screenGroup != null) screenGroup.DOFade(0f, fadeDuration);

        if (foreground != null)
        {
            float exitDuration = ExitDuration();

            Debug.Log($"[Travel] Foreground leaving to the left in {exitDuration:F2}s");

            yield return foreground.DOMove(coveredPosition - hiddenOffset, exitDuration)
                .SetEase(Ease.Linear)
                .WaitForCompletion();

            foreground.gameObject.SetActive(false);
        }
        else if (fallbackExitDuration > 0f)
        {
            yield return new WaitForSeconds(fallbackExitDuration);
        }

        HideScreen();

        // The fight only starts once the foreground is gone (and after a short beat).
        if (battleStartDelay > 0f) yield return new WaitForSeconds(battleStartDelay);

        isPlaying = false;
        Debug.Log("[Travel] Arrived - the next battle can start");
        onArrived?.Invoke();
    }

    /// <summary>
    /// Speed the foreground slides in at: the ground layer's live scroll speed times
    /// <see cref="entrySpeedFactor"/>, so it moves just a little faster than the world.
    /// </summary>
    float EntrySpeed()
    {
        if (groundLayer == null || parallax == null) return 0f;

        // World speed = the layer's authored speed times the parallax speed multiplier.
        float groundSpeed = groundLayer.speed * parallax.speedMultiplier;

        // The end screen plays after the world braked to a stop: use the full-speed value
        // so the foreground still enters at travel speed.
        if (groundSpeed <= 0.01f) groundSpeed = groundLayer.speed * parallax.baseSpeed;

        return Mathf.Max(0f, groundSpeed * entrySpeedFactor);
    }

    /// <summary>How long the foreground needs to slide from its hidden spot to its scene position.</summary>
    float EntryDuration()
    {
        float speed = EntrySpeed();
        if (speed <= 0.01f) return Mathf.Max(0.01f, fallbackEntryDuration);

        float distance = Vector3.Distance(coveredPosition + hiddenOffset, coveredPosition);
        return Mathf.Max(0.01f, distance / speed);
    }

    /// <summary>
    /// How long the foreground needs to leave to the left. It is a foreground object passing
    /// by, so it keeps the speed it entered with.
    /// </summary>
    float ExitDuration()
    {
        float speed = EntrySpeed();
        if (speed <= 0.01f) return Mathf.Max(0.01f, fallbackExitDuration);

        float distance = Vector3.Distance(coveredPosition, coveredPosition - hiddenOffset);
        return Mathf.Max(0.01f, distance / speed);
    }

    void PrepareForTravel(float from)
    {
        HideScreen();

        if (screenGroup != null) screenGroup.blocksRaycasts = false;

        if (barFill != null) barFill.fillAmount = Mathf.Clamp01(from);

        if (endInfo != null) endInfo.gameObject.SetActive(false);

        // The bar may have been switched off in the scene (e.g. to keep the Scene view clean) -
        // the transition has to bring it back before it fades it in.
        if (screen != null)
        {
            screen.gameObject.SetActive(true);
            screen.SetAsLastSibling();
        }
    }

    void ShowEndInfo()
    {
        if (endInfo == null) return;

        endInfo.gameObject.SetActive(true);

        if (endGroup != null)
        {
            endGroup.alpha = 0f;
            endGroup.DOFade(1f, Mathf.Max(0.01f, fadeDuration));
        }

        // The restart button must be clickable.
        if (screenGroup != null) screenGroup.blocksRaycasts = true;
    }

    void HideScreen()
    {
        if (screenGroup == null) return;

        screenGroup.alpha = 0f;
        screenGroup.blocksRaycasts = false;
    }

    // ------------------------------------------------------------------ restart

    void HandleRestart()
    {
        G.Get<AudioManager>()?.PlaySfx("blipSelect");

        var action = restartAction;
        restartAction = null;

        if (action != null)
        {
            action();
            return;
        }

        // The transition sits in a scene without an owner (e.g. a test scene): reload it.
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }
}
