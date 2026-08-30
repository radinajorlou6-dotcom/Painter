using UnityEngine;

/// <summary>
/// A room the camera frames to fit. Box one of these over the room's interior and the camera sizes
/// itself so its view sits entirely inside the walls, then <see cref="CameraRoomConfiner"/> stops
/// it panning past them. Walk out and the level's default framing takes over again.
///
/// This is the alternative to picking a zoom multiplier by hand: the room's own dimensions decide
/// the framing, so a corridor comes out tight and a hall comes out wide with nothing to tune, and
/// resizing the room in the editor re-frames it automatically.
///
/// Fitting means the *view* goes inside the room, not the room inside the view — the camera never
/// shows anything beyond the walls. Because a 16:9 view is much wider than it is tall, a room's
/// height is usually what decides the size, and the camera then pans left and right within it.
/// </summary>
public class CameraRoom : CameraFramingZone
{
    [Header("Fit")]
    [Tooltip("World-space margin held between the edge of the view and the room's walls. Also what " +
             "absorbs CameraShake, which is applied after the confine and would otherwise peek " +
             "past a wall during a hit. Half a tile is usually enough.")]
    [SerializeField] private float padding = 0.5f;

    [Tooltip("Never frame wider than this fraction of the level's default. A big hall zooming " +
             "further out than the rest of the game reads as a mistake, so 1 keeps it level.")]
    [SerializeField] private float maxSizeMultiplier = 1f;

    [Tooltip("Never frame tighter than this fraction of the level's default, so a cramped room " +
             "can't push the camera so close that the player loses their footing.")]
    [SerializeField] private float minSizeMultiplier = 0.35f;

    private Collider2D volume;

    /// <summary>
    /// The room's interior, inset by the padding. This is the rectangle the camera's view is kept
    /// inside — both the size fit here and the position clamp in <see cref="CameraRoomConfiner"/>
    /// read it, so the two can't disagree about where the walls are.
    /// </summary>
    public Bounds RoomBounds
    {
        get
        {
            if (volume == null) TryGetComponent(out volume);
            return CameraConfine.Inset(volume, padding, transform.position);
        }
    }

    public override float GetTargetSize(float defaultSize, float aspect)
    {
        Bounds bounds = RoomBounds;

        // Orthographic size is the view's half-height, and its half-width is that times the aspect.
        // Fitting inside means both have to clear the room, so the tighter constraint wins.
        float fitToHeight = bounds.extents.y;
        float fitToWidth = aspect > Mathf.Epsilon ? bounds.extents.x / aspect : fitToHeight;
        float fit = Mathf.Min(fitToHeight, fitToWidth);

        return Mathf.Clamp(fit, defaultSize * minSizeMultiplier, defaultSize * maxSizeMultiplier);
    }

    /// <summary>Draws the padded rectangle too, since that's what the camera actually fits to.</summary>
    private void OnDrawGizmosSelected()
    {
        Bounds bounds = RoomBounds;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 1f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
