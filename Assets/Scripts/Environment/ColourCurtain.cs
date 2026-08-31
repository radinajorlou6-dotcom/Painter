using System.Collections;
using UnityEngine;

/// <summary>
/// A blank sheet drawn over the level that fades away when its paint colour is collected,
/// revealing the coloured art underneath.
///
/// This is the way to get a white "unpainted" look out of art that is already coloured. Tinting
/// can't do it — <c>Tilemap.color</c> and <c>SpriteRenderer.color</c> are a per-channel multiply,
/// so they only ever darken, and no tint value turns blue art white. Covering it does.
///
/// Works on anything with renderers under it: a single white sprite stretched over the room, or a
/// duplicate tilemap painted in the same shapes with a white tileset. It fades every SpriteRenderer
/// and Tilemap it finds, so both behave the same.
///
/// Sorting is what makes or breaks it — the curtain has to draw *after* the art it hides and
/// *before* the player, or it either does nothing or paints over the player too. See the class
/// docs on <see cref="ColourReveal"/> for the tint-based approach where the art allows it.
/// </summary>
[DisallowMultipleComponent]
public class ColourCurtain : MonoBehaviour
{
    [Header("Colour")]
    [Tooltip("Collecting this colour lifts the curtain.")]
    [SerializeField] private PaintColour colour;

    [Tooltip("What the covered area looks like while the colour is still locked. White reads as " +
             "unpainted canvas; the alpha is the starting opacity, so drop it below 1 for a wash " +
             "that lets some of the art show through.")]
    [SerializeField] private Color curtainColour = Color.white;

    [Header("Reveal")]
    [Tooltip("Seconds for the curtain to fade. Leave at -1 to use the Drained Palette's duration. " +
             "0 removes it instantly.")]
    [SerializeField] private float fadeDuration = -1f;

    [Tooltip("Switch the object off once it has finished fading, so it costs nothing to draw for " +
             "the rest of the level. Untick only if something else on this object still has work " +
             "to do after the reveal.")]
    [SerializeField] private bool disableWhenRevealed = true;

    private ColourTint.Targets targets;
    private Coroutine fadeRoutine;
    private bool revealed;
    private bool started;

    private void OnEnable()
    {
        GameManager.OnColourUnlocked += HandleColourUnlocked;
        if (started) ApplyCurrentState();
    }

    private void OnDisable()
    {
        GameManager.OnColourUnlocked -= HandleColourUnlocked;
    }

    /// <summary>
    /// Start rather than Awake, for the same reason ColourReveal uses it: a curtain built from a
    /// ParallaxLayer's cloned children needs those clones to exist before the renderers are
    /// collected, and component Awake order within a GameObject is undefined.
    /// </summary>
    private void Start()
    {
        targets = new ColourTint.Targets(gameObject);
        if (targets.Count == 0)
        {
            DebugUtils.LogWarning(
                $"ColourCurtain on '{name}' found no sprite renderers or tilemaps to fade. Put it " +
                "on the object that holds the white art, not on an empty parent.");
            return;
        }

        started = true;
        ApplyCurrentState();
    }

    /// <summary>
    /// Snaps to the correct state with no fade. Loading into a level where the colour was already
    /// collected should look like the curtain was never there, rather than replaying the reveal.
    /// </summary>
    private void ApplyCurrentState()
    {
        revealed = GameManager.Instance != null && GameManager.Instance.IsColourUnlocked(colour);

        if (revealed)
        {
            Hide();
            return;
        }

        targets.Apply(curtainColour);
    }

    private void HandleColourUnlocked(PaintColour colourName)
    {
        if (colourName != colour || revealed) return;

        revealed = true;
        DebugUtils.Log($"Curtain '{name}' lifting for {colour}");

        // The event can land before Start on the frame a scene loads; Start will hide it directly.
        if (!started) return;

        if (!isActiveAndEnabled)
        {
            Hide();
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float duration = ColourTint.ResolveFade(fadeDuration);
        if (duration <= 0f)
        {
            Hide();
            yield break;
        }

        Color transparent = new Color(curtainColour.r, curtainColour.g, curtainColour.b, 0f);

        // Unscaled, so the reveal still plays out through a HitStop freeze or a pause — collecting
        // a colour is a beat the player should always get to watch.
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            targets.Apply(Color.Lerp(curtainColour, transparent, elapsed / duration));
            yield return null;
        }

        fadeRoutine = null;
        Hide();
    }

    /// <summary>
    /// Fully transparent first, then optionally switched off. Both, rather than either: the alpha
    /// is what matters if the object is left active, and deactivating stops it being drawn at all.
    /// </summary>
    private void Hide()
    {
        targets?.Apply(new Color(curtainColour.r, curtainColour.g, curtainColour.b, 0f));
        if (disableWhenRevealed) gameObject.SetActive(false);
    }
}
