using UnityEngine;

/// <summary>
/// The rectangle the camera is allowed to look at across a whole level. Acts as the fallback for
/// <see cref="CameraRoomConfiner"/> everywhere a <see cref="CameraRoom"/> doesn't cover, so the
/// view can't wander off the end of the tilemap into empty space while the player walks the edge.
///
/// One per scene. Unlike a framing zone this isn't a trigger and never changes the zoom — it only
/// says where the camera may point, so the level keeps its normal framing throughout.
///
/// It confines to the collider's bounding box, not its outline. A level-wide rectangle is what this
/// is for; interior walls are a <see cref="CameraRoom"/>'s job, because stopping the camera seeing
/// past those needs the framing to tighten as well as the position to clamp.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CameraBounds : MonoBehaviour
{
    public static CameraBounds Instance { get; private set; }

    [Tooltip("World-space margin held between the edge of the view and the edge of this box. " +
             "Leave at 0 to let the camera reach the very edge of the level.")]
    [SerializeField] private float padding;

    [Header("Gizmo")]
    [SerializeField] private Color gizmoColour = new Color(0.4f, 1f, 0.5f, 0.9f);

    private Collider2D volume;

    /// <summary>The region the camera's view is kept inside, inset by the padding.</summary>
    public Bounds Area
    {
        get
        {
            if (volume == null) TryGetComponent(out volume);
            return CameraConfine.Inset(volume, padding, transform.position);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DebugUtils.LogWarning(
                $"More than one CameraBounds in the scene — '{name}' is being ignored in favour of " +
                $"'{Instance.name}'. Use CameraRoom volumes for areas within a level.");
            // Only remove the duplicate component, never the GameObject it lives on.
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // The confiner is what actually enforces this box, and it normally arrives with
        // CameraZoomController — but that only bootstraps when the player enters a framing zone.
        // A level with bounds and no rooms would otherwise have nothing enforcing anything, which
        // looks configured and does nothing at all.
        CameraRoomConfiner.EnsureInstalled();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDrawGizmos()
    {
        Bounds area = Area;
        Gizmos.color = gizmoColour;
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
