using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Keeps the camera's view inside whatever region currently governs it — the <see cref="CameraRoom"/>
/// the player is standing in, or the level's <see cref="CameraBounds"/> when they're between rooms.
/// Without it the camera tracks the player straight through walls and shows whatever is behind them.
///
/// It runs as a *post*-Body callback, so the composer's lookahead and dead zone are already baked
/// into the position it corrects. That's what stops lookahead peeking past a wall: it isn't
/// disabled, it's contained.
///
/// What it eases is the *correction* — the gap between where the composer wants the camera and
/// where the region allows it — rather than the position itself. Clamping outright would be correct
/// and look terrible: crossing a threshold would yank the camera off the player in a single frame
/// while the zoom was still easing. Damping the correction instead means entering a room glides the
/// camera into place alongside the zoom, and one mechanism covers every case: entering a region
/// grows the correction from zero, leaving one shrinks it back, and moving between two slides from
/// one to the other without any special handling.
///
/// The trade is that the confine isn't strict during the transition, so a sliver beyond the wall
/// can show for a fraction of a second on the way in. Widen the region's Padding if you catch it.
///
/// Lives on the CinemachineCamera alongside <see cref="CameraShake"/>.
/// <see cref="CameraZoomController"/> adds it automatically, so there's nothing to set up.
/// </summary>
public class CameraRoomConfiner : CinemachineExtension
{
    private Vector3 currentOffset;
    private Vector3 offsetVelocity;
    private bool initialised;

    /// <summary>
    /// Puts a confiner on the scene's camera if it hasn't got one. Anything that defines a region
    /// has to call this, because a region nothing enforces is worse than no region at all — it
    /// looks configured and does nothing. Safe to call repeatedly.
    ///
    /// CinemachineExtension connects itself to the vcam in its Awake, which AddComponent triggers,
    /// so one added at runtime wires up exactly like an authored one.
    /// </summary>
    public static void EnsureInstalled(CinemachineCamera camera = null)
    {
        if (camera == null) camera = FindAnyObjectByType<CinemachineCamera>();
        if (camera == null)
        {
            DebugUtils.LogWarning(
                "No CinemachineCamera in the scene, so nothing can confine the camera. " +
                "Level 1's vcam being disabled is one way to hit this.");
            return;
        }

        if (!camera.TryGetComponent(out CameraRoomConfiner _))
        {
            camera.gameObject.AddComponent<CameraRoomConfiner>();
        }
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        // Body is the stage the position composer writes, and it runs before Finalize, where
        // CameraShake adds its offset. Correcting here means shake sits on top of a confined
        // position rather than the two arguing over the last word — and the region's padding is
        // what absorbs the handful of pixels of shake that then reach past a wall.
        if (stage != CinemachineCore.Stage.Body) return;

        Vector3 rawPosition = state.RawPosition;
        Vector3 desiredOffset = Vector3.zero;

        if (TryGetRegion(out Bounds region))
        {
            float halfHeight = state.Lens.OrthographicSize;
            float aspect = state.Lens.Aspect > Epsilon ? state.Lens.Aspect : 16f / 9f;
            desiredOffset = CameraConfine.Clamp(region, rawPosition, halfHeight * aspect, halfHeight)
                            - rawPosition;
        }

        // A cut — Cinemachine passes a negative deltaTime — or the very first frame has no previous
        // position worth easing from. The camera is being placed rather than moved, so take the
        // whole correction and start the level already framed instead of drifting into it.
        if (!initialised || deltaTime < 0f)
        {
            currentOffset = desiredOffset;
            offsetVelocity = Vector3.zero;
            initialised = true;
        }
        else
        {
            // Shares the active zone's transition time, so the camera arrives at the region's edge
            // and settles at its framing together rather than one chasing the other.
            currentOffset = Vector3.SmoothDamp(
                currentOffset, desiredOffset, ref offsetVelocity,
                CameraZoomController.FramingSmoothTime);
        }

        state.RawPosition = rawPosition + currentOffset;
    }

    /// <summary>
    /// A room the player is standing in wins over the level bounds, so a corridor can hold the
    /// camera tighter than the level as a whole ever would.
    /// </summary>
    private static bool TryGetRegion(out Bounds region)
    {
        CameraRoom room = CameraZoomController.ActiveRoom;
        if (room != null)
        {
            region = room.RoomBounds;
            return true;
        }

        if (CameraBounds.Instance != null)
        {
            region = CameraBounds.Instance.Area;
            return true;
        }

        region = default;
        return false;
    }
}

/// <summary>
/// The geometry shared by the things that pen the camera in — <see cref="CameraRoom"/>,
/// <see cref="CameraBounds"/> and the confiner itself. Keeping it in one place is what stops the
/// region a room sizes itself to drifting apart from the region the camera is actually held inside.
/// </summary>
public static class CameraConfine
{
    /// <summary>A collider's bounding box pulled in by <paramref name="padding"/> on every side.</summary>
    public static Bounds Inset(Collider2D volume, float padding, Vector3 fallbackCentre)
    {
        if (volume == null) return new Bounds(fallbackCentre, Vector3.zero);

        Bounds bounds = volume.bounds;
        bounds.extents = new Vector3(
            Mathf.Max(0.01f, bounds.extents.x - padding),
            Mathf.Max(0.01f, bounds.extents.y - padding),
            bounds.extents.z);
        return bounds;
    }

    /// <summary>Where the camera would sit if the region's walls were enforced outright.</summary>
    public static Vector3 Clamp(Bounds region, Vector3 position, float halfWidth, float halfHeight)
    {
        // A region too small to pan within on an axis gets centred on it. That case is reachable
        // whenever the size clamps hold the view wider than the room, and clamping instead would
        // mean a minimum above the maximum — nonsense the camera would jitter between.
        position.x = region.size.x <= halfWidth * 2f
            ? region.center.x
            : Mathf.Clamp(position.x, region.min.x + halfWidth, region.max.x - halfWidth);

        position.y = region.size.y <= halfHeight * 2f
            ? region.center.y
            : Mathf.Clamp(position.y, region.min.y + halfHeight, region.max.y - halfHeight);

        return position;
    }
}
