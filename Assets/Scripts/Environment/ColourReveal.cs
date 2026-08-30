using System.Collections;
using UnityEngine;

/// <summary>
/// Drains an object of colour until its paint colour is collected, then lets it bloom back in.
/// Goes on anything whose art is already authored in its final colours: tilemaps, parallax layers,
/// enemies, props.
///
/// It is deliberately visual only. <see cref="ColourControl"/> and its subclasses still own what a
/// colour unlock does to *colliders* — whether a tilemap stops blocking, whether water becomes
/// swimmable — and this owns what it does to pixels. Splitting them is what lets one component
/// cover a tilemap and a sprite alike, and what stops the two fighting over the same tint.
///
/// For colouring individual tiles *within* one tilemap, use <see cref="TilemapColourMask"/>
/// instead. Don't run both on the same tilemap: Unity multiplies a tile's own colour by the
/// tilemap-wide colour, so the two tints would compound.
/// </summary>
[DisallowMultipleComponent]
public class ColourReveal : MonoBehaviour
{
    [Header("Colour")]
    [Tooltip("Which paint colour brings this object back to life.")]
    [SerializeField] private PaintColour colour;

    [Tooltip("How it looks once restored. White leaves the art's own colours alone, which is what " +
             "you want for anything already painted the way it should end up. The monochrome " +
             "tileset is the exception — there this IS the colour, because the art is a white mask.")]
    [SerializeField] private Color unlockedColour = Color.white;

    [Header("Drained Look")]
    [Tooltip("Use a locked tint just for this object instead of the project's Drained Palette.")]
    [SerializeField] private bool overrideDrainedTint;
    [SerializeField] private Color drainedTintOverride = new Color(0.42f, 0.42f, 0.46f, 1f);

    [Tooltip("Seconds for colour to wash in. Leave at -1 to use the palette's duration. " +
             "0 snaps instantly.")]
    [SerializeField] private float fadeDuration = -1f;

    private ColourTint.Targets targets;
    private Coroutine fadeRoutine;
    private bool revealed;
    private bool started;

    private void OnEnable()
    {
        GameManager.OnColourUnlocked += HandleColourUnlocked;

        // Pooled objects come back from the pool still wearing whatever tint they were wearing when
        // they were despawned, so re-assert the state — but only once Start has been through, since
        // before that there's nothing collected to apply it to.
        if (started) ApplyCurrentState();
    }

    private void OnDisable()
    {
        GameManager.OnColourUnlocked -= HandleColourUnlocked;
    }

    /// <summary>
    /// Start rather than Awake, for two reasons. ParallaxLayer builds its looping clones during
    /// its own Awake and component order within a GameObject is undefined, so only by Start are
    /// the clones guaranteed to exist and get tinted with the original. And GameManager may not
    /// have restored the save yet during Awake.
    /// </summary>
    private void Start()
    {
        targets = new ColourTint.Targets(gameObject);
        if (targets.Count == 0)
        {
            DebugUtils.LogWarning($"ColourReveal on '{name}' found no tilemaps or sprite renderers to tint.");
            return;
        }

        started = true;
        ApplyCurrentState();
    }

    /// <summary>
    /// Snaps to whichever state is correct right now, with no fade. Loading into a level where the
    /// colour was already collected should look like it was always that way, not like it is being
    /// restored again on arrival.
    /// </summary>
    private void ApplyCurrentState()
    {
        revealed = IsUnlocked();
        targets.Apply(revealed ? ResolveUnlocked() : ResolveDrained());
    }

    /// <summary>
    /// Asks whether the colour is unlocked rather than whether its bucket is empty. The two come
    /// apart: GameManager.UnlockColour adds to unlockedColours without touching bucketStates, so a
    /// colour granted that way reports a full bucket and would leave this object drained forever.
    /// </summary>
    private bool IsUnlocked()
    {
        return GameManager.Instance != null && GameManager.Instance.IsColourUnlocked(colour);
    }

    private void HandleColourUnlocked(PaintColour colourName)
    {
        if (colourName != colour || revealed) return;

        revealed = true;
        DebugUtils.Log($"'{name}' restoring for {colour}");

        // The event can arrive before Start on the frame a scene loads, in which case there is
        // nothing collected yet — Start will apply the unlocked state directly.
        if (!started) return;

        if (!isActiveAndEnabled)
        {
            targets.Apply(ResolveUnlocked());
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        Color from = ResolveDrained();
        Color to = ResolveUnlocked();
        float duration = ColourTint.ResolveFade(fadeDuration);

        if (duration <= 0f)
        {
            targets.Apply(to);
            fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            targets.Apply(Color.Lerp(from, to, elapsed / duration));
            yield return null;
        }

        targets.Apply(to);
        fadeRoutine = null;
    }

    private Color ResolveDrained() => ColourTint.ResolveDrained(overrideDrainedTint, drainedTintOverride);

    private Color ResolveUnlocked() => ColourTint.EnsureOpaque(unlockedColour, this);
}
