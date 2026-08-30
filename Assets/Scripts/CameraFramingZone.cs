using UnityEngine;

/// <summary>
/// Shared behaviour for the trigger volumes that tell the camera how to frame part of a level:
/// <see cref="CameraZoomZone"/> for a fixed nudge in or out, <see cref="CameraRoom"/> for framing
/// fitted to a room's walls.
///
/// Both stack in <see cref="CameraZoomController"/> and the most recently entered one wins, so
/// overlapping them at a doorway resolves cleanly in both directions — walk in and the inner one
/// takes over, walk back out and the outer one picks up again.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class CameraFramingZone : MonoBehaviour
{
    [Tooltip("Seconds to ease in when the player enters. Long enough that it reads as the room " +
             "changing rather than the camera snapping — 0.3 is brisk, 1.0 is cinematic.")]
    [SerializeField] protected float transitionTime = 0.4f;

    [Header("Gizmo")]
    [SerializeField] private Color gizmoColour = new Color(0.35f, 0.75f, 1f, 0.2f);

    public float TransitionTime => transitionTime;

    /// <summary>
    /// The orthographic size the camera should ease toward while the player is inside this volume.
    /// </summary>
    /// <param name="defaultSize">The level's authored framing, read off the vcam at startup.</param>
    /// <param name="aspect">Viewport width over height.</param>
    public abstract float GetTargetSize(float defaultSize, float aspect);

    protected virtual void Reset()
    {
        // Trigger volumes only — a solid one would wall the player out of the space it's framing.
        GetComponent<Collider2D>().isTrigger = true;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        DebugUtils.Log($"Camera framing zone '{name}' entered");
        CameraZoomController.Enter(this);
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        CameraZoomController.Exit(this);
    }

    protected virtual void OnDisable()
    {
        // Same reason TutorialZone clears its flag on disable: staying registered would strand the
        // camera framed on a space the player has left, if this object is switched off or the
        // scene unloads on a respawn while they're still standing inside the volume.
        CameraZoomController.Exit(this);
    }

    /// <summary>Draws the volume in the Scene view so it can be placed and sized by eye.</summary>
    protected virtual void OnDrawGizmos()
    {
        if (!TryGetComponent(out Collider2D volume)) return;

        Bounds bounds = volume.bounds;
        Gizmos.color = gizmoColour;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(gizmoColour.r, gizmoColour.g, gizmoColour.b, 1f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
