using UnityEngine;

/// <summary>
/// One depth slice of a scrolling background. Put the art on a child of this object, set how far
/// away the layer should feel, and <see cref="ParallaxController"/> does the rest.
///
/// A rig is just a stack of these:
/// <code>
/// Parallax
/// ├── Sky        [factor (0.90, 0.05)  loop X ✓]
/// │   └── Sprite
/// ├── Hills      [factor (0.70, 0.02)  loop X ✓]
/// │   └── Sprite
/// ├── Buildings  [factor (0.45, 0.00)  loop X ✓]
/// │   └── Sprite
/// ├── Statue     [factor (0.15, 0.00)  loop X ✗]   ← a one-off landmark
/// │   └── Sprite
/// └── Grass      [factor (-0.30, 0.00) loop X ✓]   ← drawn in front of the player
///     └── Sprite
/// </code>
///
/// A layer in front of the player is the same component with the sign flipped. A negative factor
/// makes it sweep past faster than the level, which is exactly what something nearer than the
/// gameplay plane does; put it on the "In front" sorting layer and give it a negative Z so it sits
/// between the camera and the world.
///
/// The art lives on a *child* rather than on this object because looping works by cloning the art,
/// and cloning this object would clone the component along with it. Keeping the repeat unit at the
/// child's local origin also keeps the loop maths honest — the snap below moves this root, and it
/// assumes the root is roughly where the art is centred.
///
/// A layer can hold as many sprites as it likes. If it doesn't loop, just parent them all and they
/// travel together. If it does loop, group the ones that should repeat under a single child and
/// point Repeat Unit at it — the whole group then tiles as one composition:
/// <code>
/// Hills           [ParallaxLayer  loop X ✓  repeatUnit → Tile]
/// └── Tile                     ← the repeating composition
///     ├── Hill
///     ├── Tree
///     └── Fence
/// </code>
/// </summary>
[DisallowMultipleComponent]
public class ParallaxLayer : MonoBehaviour
{
    [Header("Parallax")]
    [Tooltip("How much of the camera's movement this layer copies, per axis.\n\n" +
             "1 = glued to the camera: never appears to move, an infinitely distant sky.\n" +
             "0 = pinned in the world: sweeps past at full speed, reading at the same depth as the " +
             "gameplay tilemaps.\n" +
             "Negative = in front of the gameplay plane: sweeps past FASTER than the level, so it " +
             "reads as being between the camera and the player. Try -0.2 to -0.5.\n\n" +
             "Bigger means further away. Y is usually far weaker than X, or 0 — vertical parallax " +
             "reads as the whole world sliding if you overdo it.")]
    [SerializeField] private Vector2 parallaxFactor = new Vector2(0.5f, 0f);

    [Header("Auto Scroll")]
    [Tooltip("Constant drift in world units per second, on top of the parallax. Clouds sliding, " +
             "fog creeping. Uses scaled time, so it holds still during a HitStop freeze frame " +
             "along with everything else.")]
    [SerializeField] private Vector2 autoScrollSpeed = Vector2.zero;

    [Header("Looping")]
    [Tooltip("Repeat the art sideways forever. Leave off for a one-off landmark that should only " +
             "appear once in the level.")]
    [SerializeField] private bool loopX;
    [Tooltip("Repeat the art vertically forever. Rarely wanted in a platformer; combining it with " +
             "Loop X builds a grid of copies.")]
    [SerializeField] private bool loopY;
    [Tooltip("The child that gets repeated. It can hold a whole group of sprites, not just one — " +
             "the tile size is measured across everything under it.\n\n" +
             "Leave empty only when the layer has a single child holding the art. With several " +
             "children you must set this, or only the first one tiles.")]
    [SerializeField] private Transform repeatUnit;
    [Tooltip("Size of one tile in world units. Leave at 0 to measure it from the repeat unit's " +
             "renderer bounds. Set it by hand when the art has deliberate transparent padding — " +
             "a sprite's bounds cover only its opaque pixels, so measuring would tile it too tightly.")]
    [SerializeField] private Vector2 spanOverride = Vector2.zero;

    [Tooltip("How many screens' worth of copies to build. Copies are laid out once at startup from " +
             "the camera's size at that moment, so a CameraZoomZone that pushes the view out later " +
             "can reach past the end of the row. Raise this to at least the largest zoom-out " +
             "multiplier any zone in the level uses.")]
    [SerializeField] private float loopCoverage = 1.5f;

    [Header("Follow Limits")]
    [Tooltip("Stop following once the camera passes a point on the map. Past it the layer freezes " +
             "exactly where it was and the camera walks away from it, which is how one background " +
             "hands over to another across a level.\n\n" +
             "Per axis so a side-scroller can pin the horizontal handover and leave vertical free.")]
    [SerializeField] private bool limitFollowX;
    [SerializeField] private bool limitFollowY;

    [Tooltip("The stretch of the map, in WORLD camera positions, over which this layer still " +
             "follows. Only the axes ticked above are used. Leave one side far out (-999 / 999) " +
             "for a one-sided limit.")]
    [SerializeField] private Vector2 followMin = new Vector2(-999f, -999f);
    [SerializeField] private Vector2 followMax = new Vector2(999f, 999f);

    [Tooltip("How far past the boundary the layer takes to fade away, so neighbouring backdrops " +
             "cross-fade instead of both being on screen at the handover. 0 cuts instantly, which " +
             "is fine when the boundary sits behind a wall.")]
    [SerializeField] private float fadeOutDistance = 10f;

    [Header("Room")]
    [Tooltip("Bind this layer to one room and it stops drifting across the level.\n\n" +
             "Left empty, a layer measures its parallax from wherever the camera started the level, " +
             "so it slides further and further from where you placed it the longer the player " +
             "walks. Fine for a sky that spans everything, useless for a backdrop that belongs to " +
             "one room.\n\n" +
             "Set, it measures from the room's centre instead. The art sits exactly where you " +
             "placed it when the camera is centred in the room, and drifts only as far as the " +
             "camera can pan inside it — which the room confine already limits.\n\n" +
             "Loop X works with this: the tiling repeats across the room and then freezes at its " +
             "edge rather than following the camera out. That's the combination you want for a " +
             "room too wide to cover with one piece of art.")]
    [SerializeField] private CameraRoom room;

    [Tooltip("Switch the layer's renderers off while the player is in a different room. Usually " +
             "unnecessary — art sized to its own room is off screen anyway once the camera is " +
             "confined elsewhere — and it can pop mid-transition through a doorway, so leave it " +
             "off unless you catch one room's backdrop showing in another.")]
    [SerializeField] private bool hideOutsideRoom;

    [Header("Sorting")]
    [Tooltip("Push a sorting layer and order onto every renderer under this object. Backgrounds " +
             "drawing in front of the level is the classic parallax bug; setting it in one place " +
             "per layer means the loop copies can't disagree with the original either.")]
    [SerializeField] private bool overrideSorting = true;
    [SerializeField] private string sortingLayerName = "Background";
    [Tooltip("Furthest layer gets the most negative value.")]
    [SerializeField] private int sortingOrder;

    private Vector3 layerOrigin;
    private Vector3 cameraOrigin;
    private Vector2 autoScrollOffset;
    private Vector2 span;
    private Renderer[] renderers;

    /// <summary>
    /// False until the controller has latched this layer's origin on its first driven frame.
    /// See <see cref="ParallaxController"/> for why that can't happen in Awake.
    /// </summary>
    public bool HasOrigin { get; private set; }

    private void Awake()
    {
        // Copies are built here rather than on the first driven frame so they exist before any
        // Start() runs — BackgroundColourReveal collects renderers in Start and needs to see them.
        // Only the *origin* has to wait for the camera to settle; the viewport size doesn't.
        BuildLoopCopies();

        // After the copies, so they're included in both the sorting and the room visibility toggle.
        renderers = GetComponentsInChildren<Renderer>(true);
        if (overrideSorting) ApplySorting();
    }

    private void OnEnable()
    {
        ParallaxController.Register(this);
    }

    private void OnDisable()
    {
        ParallaxController.Unregister(this);
    }

    /// <summary>
    /// Records where this layer and the camera both were, so all later movement is measured as an
    /// absolute offset from that pair rather than accumulated frame by frame. Accumulation drifts
    /// over a long level and turns a camera teleport (a checkpoint respawn) into a guess.
    /// </summary>
    public void CaptureOrigin(Vector3 cameraPosition)
    {
        layerOrigin = transform.position;
        cameraOrigin = cameraPosition;
        HasOrigin = true;
    }

    /// <summary>Places the layer for this frame. Called by <see cref="ParallaxController"/>.</summary>
    public void ApplyParallax(Vector3 cameraPosition)
    {
        if (hideOutsideRoom && room != null)
        {
            SetRenderersEnabled(CameraZoomController.ActiveRoom == room);
        }

        autoScrollOffset += autoScrollSpeed * Time.deltaTime;

        // The whole follow-limit feature is this one substitution. Clamping the camera position
        // rather than the layer's own position means that past the boundary the input simply stops
        // changing, so the layer holds exactly where it was when the camera crossed — no latch, no
        // extra state, and it picks straight back up on the way back.
        Vector3 effectiveCamera = cameraPosition;

        // A room binding is the same clamp with the bounds supplied by the room. This is what lets
        // looping and a room coexist: inside the room the tile row tracks the camera and tiles
        // across it as normal, and at the edge the row freezes with the layer instead of being
        // dragged along into the next room. The camera is confined to the room anyway while the
        // player is in it, so this only ever bites once they've left.
        if (room != null)
        {
            Bounds roomBounds = room.RoomBounds;
            effectiveCamera.x = Mathf.Clamp(effectiveCamera.x, roomBounds.min.x, roomBounds.max.x);
            effectiveCamera.y = Mathf.Clamp(effectiveCamera.y, roomBounds.min.y, roomBounds.max.y);
        }

        if (limitFollowX) effectiveCamera.x = Mathf.Clamp(effectiveCamera.x, followMin.x, followMax.x);
        if (limitFollowY) effectiveCamera.y = Mathf.Clamp(effectiveCamera.y, followMin.y, followMax.y);

        ApplyFadeOut(cameraPosition);

        // A room-bound layer measures from the room's centre, so it sits where it was placed
        // whenever the camera is centred there and can only wander as far as the camera pans
        // inside the room. Everything else measures from where the camera started the level, which
        // accumulates over the whole level — right for a sky, wrong for one room's backdrop.
        Vector3 reference = room != null ? room.RoomBounds.center : cameraOrigin;

        Vector3 travel = effectiveCamera - reference;
        Vector3 target = layerOrigin;
        target.x += travel.x * parallaxFactor.x + autoScrollOffset.x;
        target.y += travel.y * parallaxFactor.y + autoScrollOffset.y;

        // Slide the whole row of copies by whole tiles until it straddles the camera again. Moving
        // by an exact multiple of the span means the tiling lands back on itself, so there is
        // nothing to see — no seam, no jump, and no cap on how far the player can walk.
        if (loopX && span.x > 0f)
        {
            // Clamped camera, not the raw one: a frozen layer whose tiling still chased the camera
            // would never actually stop. Feeding the same clamped value here freezes the row with
            // the layer, so walking on eventually reveals the end of it — which is the handover.
            target.x += Mathf.Round((effectiveCamera.x - target.x) / span.x) * span.x;
            // Keep the drift bounded too. The snap absorbs a shift of exactly one span, so wrapping
            // here is invisible — it just stops the offset growing without limit in a long session.
            autoScrollOffset.x = Mathf.Repeat(autoScrollOffset.x, span.x);
        }

        if (loopY && span.y > 0f)
        {
            target.y += Mathf.Round((effectiveCamera.y - target.y) / span.y) * span.y;
            autoScrollOffset.y = Mathf.Repeat(autoScrollOffset.y, span.y);
        }

        transform.position = target;
    }

    /// <summary>
    /// Fills out enough copies of the art to cover the screen whatever the snap does, laid out
    /// around the original. Authoring one sprite and getting an endless layer is the whole point.
    /// </summary>
    private void BuildLoopCopies()
    {
        if (!loopX && !loopY) return;

        Transform unit = ResolveRepeatUnit();
        if (unit == null)
        {
            DebugUtils.LogWarning(
                $"ParallaxLayer '{name}' has looping enabled but no child renderer to repeat, so " +
                "looping is off. Put the layer's art on a child object rather than on the layer root.");
            loopX = false;
            loopY = false;
            return;
        }

        span = MeasureSpan(unit);

        if (loopX && span.x <= Mathf.Epsilon)
        {
            DebugUtils.LogWarning($"ParallaxLayer '{name}' measured a zero width for '{unit.name}', " +
                                  "so Loop X is off. Set Span Override.x by hand.");
            loopX = false;
        }
        if (loopY && span.y <= Mathf.Epsilon)
        {
            DebugUtils.LogWarning($"ParallaxLayer '{name}' measured a zero height for '{unit.name}', " +
                                  "so Loop Y is off. Set Span Override.y by hand.");
            loopY = false;
        }
        if (!loopX && !loopY) return;

        Vector2 viewport = ViewportSize() * Mathf.Max(1f, loopCoverage);

        // Copies fan out both ways from the original, so each side needs to cover half the screen.
        // The +1 is margin: it absorbs the half-span of slack the snap leaves, and any offset from
        // the repeat unit not sitting exactly on the layer's origin.
        int stepsX = loopX ? Mathf.CeilToInt(viewport.x / (2f * span.x)) + 1 : 0;
        int stepsY = loopY ? Mathf.CeilToInt(viewport.y / (2f * span.y)) + 1 : 0;

        Transform parent = unit.parent != null ? unit.parent : transform;

        for (int x = -stepsX; x <= stepsX; x++)
        {
            for (int y = -stepsY; y <= stepsY; y++)
            {
                if (x == 0 && y == 0) continue; // that one's the original

                Transform copy = Instantiate(unit, parent);
                copy.name = $"{unit.name} ({x},{y})";
                // World space, so a scaled layer root can't skew the spacing.
                copy.position = unit.position + new Vector3(x * span.x, y * span.y, 0f);
            }
        }
    }

    /// <summary>
    /// Falls back to the first child that has art anywhere under it, so the field can be left empty
    /// for the common one-sprite layer. Once there is more than one candidate the guess stops being
    /// safe — tiling one of several sprites and letting the others sail off screen is a confusing
    /// thing to debug — so say so rather than picking silently.
    /// </summary>
    private Transform ResolveRepeatUnit()
    {
        if (repeatUnit != null && repeatUnit != transform) return repeatUnit;

        Transform found = null;
        int candidates = 0;

        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<Renderer>(true) == null) continue;

            candidates++;
            if (found == null) found = child;
        }

        if (candidates > 1)
        {
            DebugUtils.LogWarning(
                $"ParallaxLayer '{name}' has {candidates} children with art but no Repeat Unit set, " +
                $"so only '{found.name}' will tile and the rest will scroll away. Group everything " +
                "that should repeat together under one empty child and drag it into Repeat Unit.");
        }

        return found;
    }

    /// <summary>
    /// Measures the repeat unit as the combined bounds of every renderer under it, so a tile can be
    /// a whole composition — a hill with a tree and a fence on it, repeating as one piece — rather
    /// than only ever a single sprite.
    /// </summary>
    private Vector2 MeasureSpan(Transform unit)
    {
        Vector2 measured = Vector2.zero;

        // Includes a renderer on the unit itself, so a plain single-sprite child still works.
        Renderer[] renderers = unit.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            measured = new Vector2(bounds.size.x, bounds.size.y);
        }

        // A positive override always wins; 0 means "measure it".
        return new Vector2(
            spanOverride.x > 0f ? spanOverride.x : measured.x,
            spanOverride.y > 0f ? spanOverride.y : measured.y);
    }

    /// <summary>
    /// How much world the camera shows. Read live rather than assumed, because the scenes disagree
    /// about orthographic size (8.06 in Level 1 against 10.21 elsewhere).
    /// </summary>
    private Vector2 ViewportSize()
    {
        Camera cam = Camera.main;
        if (cam == null) return new Vector2(40f, 24f); // generous guess; over-tiling is harmless

        if (cam.orthographic)
        {
            float height = cam.orthographicSize * 2f;
            return new Vector2(height * cam.aspect, height);
        }

        float distance = Mathf.Abs(transform.position.z - cam.transform.position.z);
        float frustumHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return new Vector2(frustumHeight * cam.aspect, frustumHeight);
    }

    private void ApplySorting()
    {
        if (!IsValidSortingLayer(sortingLayerName))
        {
            DebugUtils.LogWarning(
                $"ParallaxLayer '{name}' wants sorting layer '{sortingLayerName}', which doesn't " +
                "exist. Add it in Project Settings > Tags and Layers, or untick Override Sorting.");
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
        }
    }

    /// <summary>
    /// Fades the layer out once the camera is past a boundary, so a backdrop handing over to its
    /// neighbour cross-fades rather than both sitting on screen at once.
    ///
    /// Overshoot is measured on the raw camera position — the clamped one stops moving at the
    /// boundary by definition, so it can't tell you how far beyond it you are.
    /// </summary>
    private void ApplyFadeOut(Vector3 cameraPosition)
    {
        if (!limitFollowX && !limitFollowY) return;

        float overshoot = 0f;
        if (limitFollowX) overshoot = Mathf.Max(overshoot, AxisOvershoot(cameraPosition.x, followMin.x, followMax.x));
        if (limitFollowY) overshoot = Mathf.Max(overshoot, AxisOvershoot(cameraPosition.y, followMin.y, followMax.y));

        // A zero distance is a deliberate hard cut, not a divide by zero.
        float alpha = fadeOutDistance <= 0f
            ? (overshoot > 0f ? 0f : 1f)
            : 1f - Mathf.Clamp01(overshoot / fadeOutDistance);

        SetRenderersAlpha(alpha);
    }

    private static float AxisOvershoot(float value, float min, float max)
    {
        if (value > max) return value - max;
        if (value < min) return min - value;
        return 0f;
    }

    /// <summary>
    /// Writes alpha and leaves RGB alone, which is what lets a layer be faded *and* drained at the
    /// same time. ColourReveal / ColourCurtain / ColourControl drive the same renderers' colour on
    /// state changes, and this runs every frame — a whole-colour write here would wipe their tint
    /// the moment the player walked near a boundary.
    /// </summary>
    private void SetRenderersAlpha(float alpha)
    {
        if (renderers == null) return;

        bool visible = alpha > 0.001f;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            // Nothing to draw at zero, so stop drawing it rather than paying for a transparent quad.
            if (renderer.enabled != visible) renderer.enabled = visible;
            if (!visible) continue;

            if (renderer is SpriteRenderer sprite)
            {
                Color colour = sprite.color;
                colour.a = alpha;
                sprite.color = colour;
            }
        }
    }

    private void SetRenderersEnabled(bool visible)
    {
        if (renderers == null) return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.enabled != visible) renderer.enabled = visible;
        }
    }

    /// <summary>
    /// Draws the follow boundaries and the width of the cross-fade. These are world coordinates,
    /// which are all but unreadable as numbers in the Inspector — seeing where the handover
    /// actually falls on the map is the difference between tuning this and guessing at it.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!limitFollowX && !limitFollowY) return;

        Vector3 centre = transform.position;
        const float reach = 40f; // how far the marker lines extend, purely so they're findable

        if (limitFollowX)
        {
            DrawBound(new Vector3(followMin.x, centre.y, 0f), Vector3.up, reach, -1f);
            DrawBound(new Vector3(followMax.x, centre.y, 0f), Vector3.up, reach, 1f);
        }

        if (limitFollowY)
        {
            DrawBound(new Vector3(centre.x, followMin.y, 0f), Vector3.right, reach, -1f);
            DrawBound(new Vector3(centre.x, followMax.y, 0f), Vector3.right, reach, 1f);
        }
    }

    /// <summary>Solid line at the boundary, dimmer one where the layer has fully faded out.</summary>
    private void DrawBound(Vector3 point, Vector3 along, float reach, float outwardSign)
    {
        Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.9f);
        Gizmos.DrawLine(point - along * reach, point + along * reach);

        if (fadeOutDistance <= 0f) return;

        Vector3 outward = new Vector3(along.y, along.x, 0f) * (fadeOutDistance * outwardSign);
        Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.25f);
        Gizmos.DrawLine(point + outward - along * reach, point + outward + along * reach);
    }

    private static bool IsValidSortingLayer(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return false;

        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.name == layerName) return true;
        }
        return false;
    }
}
