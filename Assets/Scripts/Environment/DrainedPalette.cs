using UnityEngine;

/// <summary>
/// The project-wide look of "not yet restored". One asset holds the drained tint and how long
/// colour takes to wash back in, so every locked thing in the game agrees without that value being
/// typed onto a hundred components and drifting apart between scenes.
///
/// Create it via Assets > Create > Painter > Drained Palette and save it as
/// <c>Assets/Resources/DrainedPalette.asset</c> — the Resources folder is how the components find
/// it without a reference on each one.
///
/// The asset is entirely optional. Without it everything falls back to the same values baked in
/// here, so the system works the moment the scripts compile and the asset is a later refinement
/// rather than a prerequisite.
/// </summary>
[CreateAssetMenu(fileName = "DrainedPalette", menuName = "Painter/Drained Palette")]
public class DrainedPalette : ScriptableObject
{
    /// <summary>Where <see cref="Shared"/> looks, relative to any Resources folder.</summary>
    public const string ResourcePath = "DrainedPalette";

    private static readonly Color FallbackTint = new Color(0.42f, 0.42f, 0.46f, 1f);
    private const float FallbackFade = 1.5f;

    [Tooltip("The tint a locked object wears. Multiplied over the art, so on the monochrome " +
             "tileset this is literally the colour you'll see, and on full-colour sprites it " +
             "darkens rather than drains — lean on it being dark rather than grey.")]
    [SerializeField] private Color drainedTint = new Color(0.42f, 0.42f, 0.46f, 1f);

    [Tooltip("Seconds for colour to wash back in when a paint colour is collected. Runs on " +
             "unscaled time, so the moment plays out through a HitStop freeze or a pause.")]
    [SerializeField] private float fadeDuration = 1.5f;

    public Color DrainedTint => drainedTint;
    public float FadeDuration => fadeDuration;

    private static DrainedPalette shared;
    private static bool searched;

    /// <summary>
    /// The project's palette, or null if there isn't one. Callers should go through
    /// <see cref="ColourTint"/> rather than reading this directly — it resolves the fallbacks.
    /// </summary>
    public static DrainedPalette Shared
    {
        get
        {
            // Cached including the miss: a project with no palette would otherwise hit the
            // Resources index on every locked object in every scene load.
            if (!searched)
            {
                shared = Resources.Load<DrainedPalette>(ResourcePath);
                searched = true;
            }
            return shared;
        }
    }

    /// <summary>The drained tint, or the built-in default when no palette asset exists.</summary>
    public static Color ResolveTint() => Shared != null ? Shared.drainedTint : FallbackTint;

    /// <summary>The fade duration, or the built-in default when no palette asset exists.</summary>
    public static float ResolveFade() => Shared != null ? Shared.fadeDuration : FallbackFade;
}
