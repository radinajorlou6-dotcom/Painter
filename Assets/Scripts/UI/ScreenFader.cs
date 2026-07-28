using System.Collections;
using UnityEngine;

/// <summary>
/// Fades the whole screen to/from black. Put this on a full-screen black UI Image that has a
/// CanvasGroup, on a Canvas set to Screen Space - Overlay (sorting order above everything).
/// Everything runs on unscaled time so the fade still animates while the game is frozen.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>Fade to solid black.</summary>
    public IEnumerator FadeOut(float duration) => Fade(1f, duration);

    /// <summary>Fade back to fully transparent.</summary>
    public IEnumerator FadeIn(float duration) => Fade(0f, duration);

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        // Block clicks only while (or as we become) opaque.
        canvasGroup.blocksRaycasts = targetAlpha > 0.5f;

        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }
}
