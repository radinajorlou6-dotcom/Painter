using UnityEngine;

/// <summary>
/// A door that blocks the way until its colour is restored, then lets the player walk through.
///
/// Same idea as <see cref="ColourControl"/> and inherits everything from it — the drained tint
/// while locked, the wash of colour on unlock, the palette, the save-aware check on load. The only
/// thing it changes is *what* stops blocking: ColourControl is built around a tilemap and reaches
/// for a TilemapCollider2D, which a door made of a single sprite doesn't have.
///
/// The door stays visible after unlocking. It's meant to read as a door that has been opened, not
/// one that vanished — if you want it gone, fade it out from the OnCollected-style hook of whatever
/// else is nearby, or just make the unlocked art transparent.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ColourDoor : ColourControl
{
    [Header("Door")]
    [Tooltip("Turn the collider into a trigger instead of switching it off. Use this when " +
             "something still needs to detect the player passing through; leave it off and the " +
             "door simply stops existing to physics.")]
    [SerializeField] private bool becomesTriggerWhenOpen;

    private Collider2D[] doorColliders;

    protected override void Awake()
    {
        // Cached before base.Awake, which calls Unlock() straight away when the colour has already
        // been restored — the same reason WaterZone and VineZone cache their collider first. Miss
        // this and a door the player already opened comes back solid after a reload.
        doorColliders = GetComponentsInChildren<Collider2D>(true);

        base.Awake();
    }

    protected override void Start()
    {
        base.Start();

        // A door that is a trigger while locked isn't a door. Easy to do by accident, and it fails
        // silently — the player just walks through and never learns the colour was the point.
        if (isRestored) return;

        foreach (Collider2D col in doorColliders)
        {
            if (col != null && col.enabled && !col.isTrigger) return;
        }

        DebugUtils.LogWarning(
            $"ColourDoor '{name}' has nothing solid on it while locked, so the player can already " +
            "walk through. Untick Is Trigger on its collider.");
    }

    /// <summary>
    /// Opens the way. Overrides the base rather than extending it because the base disables a
    /// TilemapCollider2D specifically, which is null here.
    /// </summary>
    protected override void Unlock()
    {
        foreach (Collider2D col in doorColliders)
        {
            if (col == null) continue;

            if (becomesTriggerWhenOpen) col.isTrigger = true;
            else col.enabled = false;
        }
    }
}
