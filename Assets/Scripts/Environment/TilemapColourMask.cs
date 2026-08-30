using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Gates individual tiles of one tilemap on different paint colours, so a single Ground map can
/// hold blue patches and green patches without being split into separate tilemaps.
///
/// Splitting it would be the obvious approach and it's the wrong one for ground: each tilemap
/// carries its own collider, so the player crosses a collider boundary walking from one patch to
/// the next, and boundaries are where footing snags. This keeps Ground as one tilemap with one
/// collider and varies only the per-cell colour.
///
/// Which cells belong to which colour is painted, not typed. Make a sibling tilemap per colour,
/// paint any tile into the cells you want claimed, and point a region at it — the mask's renderer
/// is switched off at runtime, so it's visible to work with in the editor and invisible in game.
///
/// This is colour only and never touches collision. Tiles that need to become passable stay on
/// their own tilemap with <see cref="ColourControl"/>, which is what that's for.
/// </summary>
[RequireComponent(typeof(Tilemap))]
[DisallowMultipleComponent]
public class TilemapColourMask : MonoBehaviour
{
    [Serializable]
    public class ColourRegion
    {
        [Tooltip("A sibling tilemap whose painted cells claim the matching cells of this one. " +
                 "Leave empty for the catch-all region: every cell no other region claimed.")]
        public Tilemap mask;

        [Tooltip("Which paint colour restores this region.")]
        public PaintColour colour;

        [Tooltip("How these cells look once restored. The tileset is a white mask, so this is " +
                 "literally the colour you'll see.")]
        public Color unlockedColour = Color.white;

        [Tooltip("Use a locked tint just for this region instead of the project's Drained Palette.")]
        public bool overrideDrainedTint;
        public Color drainedTintOverride = new Color(0.42f, 0.42f, 0.46f, 1f);

        [NonSerialized] public List<Vector3Int> cells = new List<Vector3Int>();
        [NonSerialized] public bool revealed;
    }

    [Tooltip("Checked in order — the first region whose mask claims a cell wins, so put the " +
             "catch-all region (the one with no mask) last.")]
    [SerializeField] private List<ColourRegion> regions = new List<ColourRegion>();

    [Tooltip("Seconds for colour to wash in. Leave at -1 to use the palette's duration.")]
    [SerializeField] private float fadeDuration = -1f;

    [Tooltip("How many times a fade repaints the tiles. Every colour write dirties the tilemap " +
             "chunk and forces it to re-mesh, so a per-frame fade on a big map stutters. Twenty " +
             "steps across a second and a half is indistinguishable and costs almost nothing.")]
    [SerializeField] private int fadeSteps = 20;

    private Tilemap tilemap;
    private bool started;

    private void Awake()
    {
        tilemap = GetComponent<Tilemap>();

        // Unity multiplies each tile's colour by the tilemap-wide colour. Per-cell colour is doing
        // all the work here, so the map-wide tint has to be neutral or every region comes out
        // tinted twice.
        tilemap.color = Color.white;

        // Masks exist to be painted in the editor, not seen in game. Switching their renderers off
        // here means nobody has to remember to do it, and the mask still shows while authoring.
        foreach (ColourRegion region in regions)
        {
            if (region?.mask == null) continue;
            if (region.mask.TryGetComponent(out TilemapRenderer maskRenderer)) maskRenderer.enabled = false;
        }
    }

    private void OnEnable()
    {
        GameManager.OnColourUnlocked += HandleColourUnlocked;
        if (started) ApplyCurrentState();
    }

    private void OnDisable()
    {
        GameManager.OnColourUnlocked -= HandleColourUnlocked;
    }

    private void Start()
    {
        BuildRegionCells();
        started = true;
        ApplyCurrentState();
    }

    /// <summary>
    /// Sorts every occupied cell into a region once. Walking cellBounds is cheap enough at load and
    /// far cheaper than asking the masks anything again during a fade.
    /// </summary>
    private void BuildRegionCells()
    {
        foreach (ColourRegion region in regions) region.cells.Clear();

        BoundsInt bounds = tilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell)) continue;

            ColourRegion owner = FindRegion(cell);
            if (owner == null) continue;

            // The tile assets ship with TileFlags.LockColor set, which makes SetColor silently do
            // nothing. Clearing it per cell is what makes any of this work, and it costs nothing
            // to do once here rather than before every write.
            tilemap.SetTileFlags(cell, TileFlags.None);
            owner.cells.Add(cell);
        }
    }

    /// <summary>First region whose mask claims the cell; the catch-all region if none do.</summary>
    private ColourRegion FindRegion(Vector3Int cell)
    {
        ColourRegion catchAll = null;

        foreach (ColourRegion region in regions)
        {
            if (region == null) continue;

            if (region.mask == null)
            {
                // Remember it but keep looking — a mask later in the list should still win, so the
                // catch-all works wherever it sits even though the tooltip asks for it last.
                if (catchAll == null) catchAll = region;
                continue;
            }

            if (region.mask.HasTile(cell)) return region;
        }

        return catchAll;
    }

    private void ApplyCurrentState()
    {
        foreach (ColourRegion region in regions)
        {
            if (region == null || region.cells.Count == 0) continue;

            region.revealed = IsUnlocked(region.colour);
            Paint(region, region.revealed ? ResolveUnlocked(region) : ResolveDrained(region));
        }
    }

    /// <summary>
    /// Asks whether the colour is unlocked rather than whether its bucket is empty — the two come
    /// apart, since GameManager.UnlockColour never touches bucketStates.
    /// </summary>
    private static bool IsUnlocked(PaintColour colour)
    {
        return GameManager.Instance != null && GameManager.Instance.IsColourUnlocked(colour);
    }

    private void HandleColourUnlocked(PaintColour colourName)
    {
        if (!started) return;

        foreach (ColourRegion region in regions)
        {
            if (region == null || region.revealed || region.colour != colourName) continue;
            if (region.cells.Count == 0) continue;

            region.revealed = true;
            DebugUtils.Log($"'{name}' restoring {region.cells.Count} tiles for {colourName}");

            if (isActiveAndEnabled) StartCoroutine(FadeIn(region));
            else Paint(region, ResolveUnlocked(region));
        }
    }

    private IEnumerator FadeIn(ColourRegion region)
    {
        Color from = ResolveDrained(region);
        Color to = ResolveUnlocked(region);
        float duration = ColourTint.ResolveFade(fadeDuration);
        int steps = Mathf.Max(1, fadeSteps);

        if (duration <= 0f)
        {
            Paint(region, to);
            yield break;
        }

        float stepDelay = duration / steps;
        for (int i = 1; i <= steps; i++)
        {
            yield return new WaitForSecondsRealtime(stepDelay);
            Paint(region, Color.Lerp(from, to, i / (float)steps));
        }

        Paint(region, to);
    }

    private void Paint(ColourRegion region, Color colour)
    {
        List<Vector3Int> cells = region.cells;
        for (int i = 0; i < cells.Count; i++)
        {
            tilemap.SetColor(cells[i], colour);
        }
    }

    private Color ResolveDrained(ColourRegion region)
    {
        return ColourTint.ResolveDrained(region.overrideDrainedTint, region.drainedTintOverride);
    }

    private Color ResolveUnlocked(ColourRegion region)
    {
        return ColourTint.EnsureOpaque(region.unlockedColour, this);
    }
}
