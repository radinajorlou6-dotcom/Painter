using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A tilemap that is drained of colour and solid until its colour is unlocked. When the matching
/// colour is restored it washes back to the tile art's own colours and its collider turns off so
/// the player can pass through.
///
/// The colour model matches <see cref="ColourReveal"/> and <see cref="TilemapColourMask"/>: you
/// choose what it looks like while **locked**, and unlocked is simply the art as drawn. That is
/// why <see cref="unlockedColour"/> defaults to white — a tint is a multiply, so white means
/// "leave the tiles exactly as the artist made them". Set it to something else only when the tiles
/// are a white mask and the tint is what supplies their colour.
///
/// Gameplay and looks are handled separately on purpose. <see cref="Unlock"/> is about colliders
/// and is what subclasses override; the tint is driven from the event handler and never needs
/// touching, so a subclass can change what unlocking *does* without restating how it looks.
/// </summary>
public class ColourControl : MonoBehaviour
{
    protected Rigidbody2D rb;
    protected Tilemap tilemap;
    protected TilemapCollider2D tilemapCollider;

    [Header("Colour")]
    [Tooltip("Which colour unlock this object reacts to.")]
    [SerializeField] protected PaintColour colour;

    [Tooltip("How it looks once restored. White leaves the tile art's own colours alone, which is " +
             "what you want for tiles that are already drawn in colour. Use an actual colour only " +
             "for white-mask tiles, where the tint is what makes them coloured at all.")]
    [SerializeField] protected Color unlockedColour = Color.white;

    [Header("Drained Look")]
    [Tooltip("Give this object its own locked tint instead of the project's Drained Palette.")]
    [SerializeField] protected bool overrideDrainedTint;
    [SerializeField] protected Color drainedTintOverride = new Color(0.42f, 0.42f, 0.46f, 1f);

    [Tooltip("Seconds for colour to wash in. Leave at -1 to use the palette's duration. 0 snaps.")]
    [SerializeField] protected float fadeDuration = -1f;

    /// <summary>True once this object's colour has been restored. Latched — it never goes back.</summary>
    protected bool isRestored;

    private ColourTint.Targets targets;
    private Coroutine fadeRoutine;

    protected virtual void OnEnable()
    {
        GameManager.OnColourUnlocked += HandleColourUnlocked;
    }

    protected virtual void OnDisable()
    {
        GameManager.OnColourUnlocked -= HandleColourUnlocked;
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        tilemap = GetComponent<Tilemap>();
        tilemapCollider = GetComponent<TilemapCollider2D>();

        // Colliders are settled in Awake so nothing can touch this object on frame one and find it
        // solid when it shouldn't be. The tint waits for Start — see below.
        isRestored = IsUnlocked();
        if (isRestored) Unlock();
    }

    /// <summary>
    /// The tint is applied here rather than in Awake so every renderer under the object exists
    /// first — the same reason ColourReveal uses Start. Nothing is visible before the first frame
    /// either way, so there is no flash to worry about.
    /// </summary>
    protected virtual void Start()
    {
        targets = new ColourTint.Targets(gameObject);
        if (targets.Count == 0)
        {
            DebugUtils.LogWarning($"ColourControl on '{name}' found no tilemap or sprites to tint.");
            return;
        }

        targets.Apply(isRestored ? ResolveUnlocked() : ResolveDrained());
    }

    /// <summary>
    /// Asks whether the colour is unlocked rather than whether its bucket is empty. The two come
    /// apart: GameManager.UnlockColour adds to unlockedColours without touching bucketStates, so a
    /// colour granted that way would leave this object locked forever.
    /// </summary>
    private bool IsUnlocked()
    {
        return GameManager.Instance != null && GameManager.Instance.IsColourUnlocked(colour);
    }

    /// <summary>
    /// What restoring the colour *does*, as opposed to what it looks like. Subclasses override
    /// this alone — <see cref="WaterZone"/> and <see cref="VineZone"/> keep the collider alive as
    /// a trigger instead of switching it off — and inherit the tint behaviour unchanged.
    /// </summary>
    protected virtual void Unlock()
    {
        if (tilemapCollider != null) tilemapCollider.enabled = false;
    }

    /// <summary>Event handler: only reacts when the unlocked colour matches ours.</summary>
    protected virtual void HandleColourUnlocked(PaintColour colourName)
    {
        if (colourName != colour || isRestored) return;

        isRestored = true;
        Unlock();

        DebugUtils.Log($"'{name}' restoring for {colour}");

        // The event can arrive before Start on the frame a scene loads, in which case Start applies
        // the restored tint directly and there is nothing to fade from.
        if (targets == null) return;

        if (!isActiveAndEnabled)
        {
            targets.Apply(ResolveUnlocked());
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(
            ColourTint.Fade(targets, ResolveDrained(), ResolveUnlocked(), ColourTint.ResolveFade(fadeDuration)));
    }

    private Color ResolveDrained() => ColourTint.ResolveDrained(overrideDrainedTint, drainedTintOverride);

    private Color ResolveUnlocked() => ColourTint.EnsureOpaque(unlockedColour, this);
}
