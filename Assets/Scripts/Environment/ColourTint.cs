using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// The bits every "drained until its colour is collected" component needs, in one place.
///
/// The project colours things two different ways — <c>Tilemap.color</c> on the tilemaps and
/// <c>SpriteRenderer.color</c> on everything else — and a component shouldn't have to care which
/// kind of object it landed on. <see cref="Targets"/> collects both and treats them the same, so
/// the same component works on a tilemap, a parallax layer and an enemy without a branch in sight.
/// </summary>
public static class ColourTint
{
    /// <summary>
    /// Every renderer under one object that can be tinted, gathered once so a fade isn't running
    /// GetComponentsInChildren every frame.
    /// </summary>
    public class Targets
    {
        private readonly List<Tilemap> tilemaps = new List<Tilemap>();
        private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();

        public int Count => tilemaps.Count + sprites.Count;

        /// <summary>
        /// Includes inactive children on purpose: a layer or prop switched off in the inspector
        /// still has to be wearing the right tint by the time something enables it.
        /// </summary>
        public Targets(GameObject root)
        {
            root.GetComponentsInChildren(true, tilemaps);
            root.GetComponentsInChildren(true, sprites);
        }

        public void Apply(Color colour)
        {
            for (int i = 0; i < tilemaps.Count; i++)
            {
                if (tilemaps[i] != null) tilemaps[i].color = colour;
            }

            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] != null) sprites[i].color = colour;
            }
        }
    }

    /// <summary>
    /// The tint a locked object should wear: its own override if it has one, otherwise the
    /// project's palette, otherwise a built-in default.
    /// </summary>
    public static Color ResolveDrained(bool useOverride, Color overrideTint)
    {
        return useOverride ? overrideTint : DrainedPalette.ResolveTint();
    }

    /// <summary>
    /// A fade duration, treating anything non-positive as "use the palette". Zero is a legitimate
    /// request for an instant change, so it's the caller that decides — this only fills in blanks.
    /// </summary>
    public static float ResolveFade(float duration)
    {
        return duration < 0f ? DrainedPalette.ResolveFade() : duration;
    }

    /// <summary>
    /// Catches the mistake that made the vines vanish: an unlocked colour left at alpha 0 is
    /// almost always a colour picker that was never given an alpha, not a request for invisibility.
    /// </summary>
    public static Color EnsureOpaque(Color colour, Object context)
    {
        if (colour.a > 0f) return colour;

        DebugUtils.LogWarning(
            $"'{context.name}' has an unlocked colour with alpha 0, which would make it invisible " +
            "once restored. Treating it as opaque — set the alpha in the inspector.");

        colour.a = 1f;
        return colour;
    }
}
