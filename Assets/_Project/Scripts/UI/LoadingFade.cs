using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cover over the whole screen at the start of a run: it hides the first frames while the
/// scene is coming up (spawned modules, the city, the audio bank) and fades away a moment
/// later. Built in code on the screen canvas, so the scene only carries this component.
/// <para>
/// The timings are unscaled on purpose: a game that boots with a frozen clock (or is paused
/// right away) must never be left behind a black screen. Keep <see cref="holdTime"/> +
/// <see cref="fadeDuration"/> shorter than the tutorial's start delay so the hand never shows
/// up on top of the fading cover.
/// </para>
/// </summary>
public class LoadingFade : MonoBehaviour, IBootstraped
{
    public bool isInitialised { get; set; }

    [Header("Cover")]
    [Tooltip("Colour of the cover (alpha is driven by the fade).")]
    [SerializeField] Color color = Color.black;

    [Tooltip("How long the cover stays fully opaque before it starts to fade.")]
    [Min(0f)]
    [SerializeField] float holdTime = 0.3f;

    [Tooltip("How long the cover takes to fade away.")]
    [Min(0f)]
    [SerializeField] float fadeDuration = 0.55f;

    [Tooltip("Swallow clicks while the cover is up, so nothing can be pressed through it.")]
    [SerializeField] bool blockInput = true;

    Image cover;
    Canvas canvas;

    /// <summary>True while the cover still covers the screen.</summary>
    public bool IsCovering => cover != null && cover.gameObject.activeSelf;

    public void Init()
    {
        canvas = ScreenCanvas.Find();

        if (canvas == null)
        {
            Debug.LogWarning("[Loading] No screen canvas - the cover cannot be shown");
            return;
        }

        BuildCover();
        StartCoroutine(Run());
    }

    void Update()
    {
        if (!IsCovering) return;

        // Anything that opens during the boot (a station panel, a tooltip) would otherwise be
        // drawn on top of the cover - the cover always stays the last sibling while it lives.
        cover.transform.SetAsLastSibling();
    }

    IEnumerator Run()
    {
        DevLog.Info($"[Loading] Cover up for {holdTime:F2}s, fading over {fadeDuration:F2}s");

        if (holdTime > 0f) yield return new WaitForSecondsRealtime(holdTime);

        if (fadeDuration > 0f)
        {
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));

                var faded = color;
                faded.a = color.a * (1f - t);

                if (cover != null) cover.color = faded;

                yield return null;
            }
        }

        if (cover == null) yield break;

        cover.gameObject.SetActive(false);
        DevLog.Info("[Loading] Cover gone");
    }

    /// <summary>Adds the full-screen image on top of the canvas and starts it fully opaque.</summary>
    void BuildCover()
    {
        var coverObject = new GameObject("LoadingFade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        coverObject.transform.SetParent(canvas.transform, false);

        cover = coverObject.GetComponent<Image>();
        cover.raycastTarget = blockInput;

        var opaque = color;
        opaque.a = color.a <= 0f ? 1f : color.a;
        cover.color = opaque;

        var rect = (RectTransform)coverObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.SetAsLastSibling();
    }
}
