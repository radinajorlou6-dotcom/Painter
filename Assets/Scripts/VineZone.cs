using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A patch of vines that only becomes climbable once its colour is unlocked.
///
/// While locked it behaves like any other <see cref="ColourControl"/> tilemap: greyed out and
/// solid, so it reads as dead scenery the player can stand on. Unlocking recolours it and converts
/// the collider into a trigger rather than switching it off, so the player can move into the vines
/// and climb them.
///
/// The trigger is the whole vine volume, so "am I on a vine" is answered by the physics broadphase
/// rather than by state kept here — nothing can desync, and there is no per-frame cost. With a
/// CompositeCollider2D every vine on the tilemap merges into one collider, so a level can have any
/// number of them and still share one component.
///
/// This replaces the plain ColourControl on the vine tilemap — don't run both.
/// </summary>
[RequireComponent(typeof(TilemapCollider2D))]
public class VineZone : ColourControl
{
    /// <summary>
    /// Raised when a player enters or leaves unlocked vines. The bool is the new state. It says
    /// the player *can* climb, not that they are — PlayerMovement owns that decision, because
    /// grabbing depends on input, on whether they let go on purpose a moment ago, and on where
    /// they are across the vine. The zone comes with it so the player can ask it about columns.
    /// </summary>
    public static event Action<PlayerMovement, VineZone, bool> PlayerVineStateChanged;

    private CompositeCollider2D compositeCollider;
    private bool isClimbable;

    protected override void Awake()
    {
        // Cache first: base.Awake unlocks straight away if the colour is already spent.
        compositeCollider = GetComponent<CompositeCollider2D>();

        // Outlines geometry is hollow — edges only, no interior — so a trigger built from it fires
        // as the player crosses the surface and then immediately exits once they're fully inside.
        // A vine has to be a filled volume to keep reporting the player while they hang in the
        // middle of it, so force it rather than relying on the inspector being set correctly.
        if (compositeCollider != null)
        {
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        base.Awake();
        SetTrigger(true);
    }

    /// <summary>
    /// Unlike the base, this keeps the collider alive and turns it into a trigger — the vine still
    /// needs to detect the player, it just stops blocking them.
    /// </summary>
    protected override void Unlock()
    {
        SetTrigger(true);
        isClimbable = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // The collider isn't a trigger while locked, so this shouldn't fire — but guard anyway
        // in case Is Trigger gets left ticked in the inspector.
        if (!isClimbable) return;

        PlayerMovement player = ResolvePlayer(other);
        if (player == null) return;

        DebugUtils.Log($"{player.name} entered vines {name}");
        PlayerVineStateChanged?.Invoke(player, this, true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isClimbable) return;

        PlayerMovement player = ResolvePlayer(other);
        if (player == null) return;

        DebugUtils.Log($"{player.name} left vines {name}");
        PlayerVineStateChanged?.Invoke(player, this, false);
    }

    /// <summary>
    /// Finds the centre line of the vine column the given point sits in, and reports whether that
    /// column actually holds a vine tile there.
    ///
    /// Overlapping the trigger is not enough to identify a column: the trigger is the merged shape
    /// of every vine on the tilemap, so a player brushing the edge of one overlaps it while their
    /// own centre is still in the empty cell next door. Asking the tilemap which cell the point is
    /// in — rather than asking the collider — is what tells those two cases apart.
    /// </summary>
    public bool TryGetColumnCentre(Vector3 worldPosition, out float centreX)
    {
        Vector3Int cell = tilemap.WorldToCell(worldPosition);
        centreX = tilemap.GetCellCenterWorld(cell).x;
        return tilemap.HasTile(cell);
    }

    /// <summary>
    /// When the tilemap feeds a CompositeCollider2D the composite owns the trigger flag and the
    /// TilemapCollider2D's own is ignored, so set whichever one is actually in charge.
    /// </summary>
    private void SetTrigger(bool value)
    {
        if (compositeCollider != null) compositeCollider.isTrigger = value;
        else tilemapCollider.isTrigger = value;
    }

    /// <summary>
    /// The trigger can be hit by a child collider of the player, so search upward from
    /// whatever actually touched us rather than assuming the script sits on it.
    /// </summary>
    private PlayerMovement ResolvePlayer(Collider2D other)
    {
        if (!other.CompareTag("Player")) return null;
        return other.GetComponentInParent<PlayerMovement>();
    }
}
