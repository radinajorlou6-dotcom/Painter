using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A body of water that only becomes swimmable once its colour is unlocked.
///
/// While locked it behaves like any other <see cref="ColourControl"/> tilemap: greyed out and
/// solid, so the player can walk across it. Unlocking recolours it and converts the collider
/// into a trigger rather than switching it off, so the player drops in and starts swimming.
///
/// The trigger is the whole water volume, so "am I in water" is answered by the physics
/// broadphase rather than by state kept here — nothing can desync, and there is no per-frame
/// cost. With a CompositeCollider2D every pool on the tilemap merges into one collider, so a
/// level can have any number of them and still share one component.
///
/// This replaces the plain ColourControl on the water tilemap — don't run both.
/// </summary>
[RequireComponent(typeof(TilemapCollider2D))]
public class WaterZone : ColourControl
{
    /// <summary>
    /// Raised when a player enters or leaves unlocked water. The bool is the new swimming state.
    /// </summary>
    public static event Action<PlayerMovement, bool> PlayerSwimStateChanged;

    private CompositeCollider2D compositeCollider;
    private bool isWater;

    protected override void Awake()
    {
        // Cache first: base.Awake unlocks straight away if the colour is already spent.
        compositeCollider = GetComponent<CompositeCollider2D>();

        // Outlines geometry is hollow — edges only, no interior — so a trigger built from it
        // fires as the player crosses the surface and then immediately exits once they're fully
        // inside. Water has to be a filled volume to keep them in it. The rest of the project's
        // tilemaps use Outlines, which is right for solid platforms but wrong here, so force it
        // rather than relying on the inspector being set correctly.
        if (compositeCollider != null)
        {
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        base.Awake();
    }

    /// <summary>
    /// Unlike the base, this keeps the collider alive and turns it into a trigger — the water
    /// still needs to detect the player, it just stops holding them up.
    /// </summary>
    protected override void Unlock()
    {
        SetTrigger(true);
        isWater = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // The collider isn't a trigger while locked, so this shouldn't fire — but guard anyway
        // in case Is Trigger gets left ticked in the inspector.
        if (!isWater) return;

        PlayerMovement player = ResolvePlayer(other);
        if (player == null) return;

        DebugUtils.Log($"{player.name} entered water {name}");
        PlayerSwimStateChanged?.Invoke(player, true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isWater) return;

        PlayerMovement player = ResolvePlayer(other);
        if (player == null) return;

        DebugUtils.Log($"{player.name} left water {name}");
        PlayerSwimStateChanged?.Invoke(player, false);
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
