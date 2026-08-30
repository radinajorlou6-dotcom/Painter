using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Eases the camera's orthographic size between whatever <see cref="CameraFramingZone"/> the player
/// is standing in and the level's default framing. Tightening the view down a corridor and opening
/// it back up in a room is a cheap way to make a doorway feel like an arrival.
///
/// It drives the lens on the one existing CinemachineCamera rather than blending between several.
/// A second vcam would be the textbook Cinemachine answer, but it doesn't survive contact with this
/// project: <see cref="CameraShake"/> is a CinemachineExtension with a singleton Instance, so a
/// copy on a second vcam destroys itself, and shake would then be applied to whichever camera
/// wasn't active. Animating one lens sidesteps that and keeps the composer's damping, lookahead
/// and dead zone exactly as they were tuned.
///
/// Writing the lens in Update is deliberate — CinemachineBrain reads it during LateUpdate, so the
/// value is always a frame-fresh input to the pipeline rather than something fighting it.
///
/// Nothing needs to place this in a scene: the first zone the player enters creates one.
/// </summary>
public class CameraZoomController : MonoBehaviour
{
    public static CameraZoomController Instance { get; private set; }

    [Header("Target")]
    [Tooltip("Leave empty to find the scene's CinemachineCamera automatically.")]
    [SerializeField] private CinemachineCamera vcam;

    [Header("Timing")]
    [Tooltip("Seconds to ease back to the level's default framing once the player leaves every " +
             "zone. Zones carry their own time for easing in.")]
    [SerializeField] private float defaultTransitionTime = 0.4f;

    private readonly List<CameraFramingZone> activeZones = new List<CameraFramingZone>();
    private float defaultSize;
    private float currentSize;
    private float sizeVelocity;
    private float currentSmoothTime = 0.4f;
    private float releaseSmoothTime;
    private bool resolved;
    private bool warnedAboutMissingCamera;

    /// <summary>
    /// How long the framing currently in progress should take. <see cref="CameraRoomConfiner"/>
    /// eases the camera into a room's walls over the same time the lens takes to reach that room's
    /// size, so the two halves of a transition move as one instead of one chasing the other.
    /// </summary>
    public static float FramingSmoothTime => Instance != null ? Instance.currentSmoothTime : 0.4f;

    /// <summary>
    /// The room the camera should currently be held inside, or null when the player isn't in one.
    /// Read by <see cref="CameraRoomConfiner"/>.
    ///
    /// This looks for the topmost <see cref="CameraRoom"/> rather than just checking the topmost
    /// zone, so dropping a CameraZoomZone inside a room to tweak the framing of one corner doesn't
    /// silently switch the walls off.
    /// </summary>
    public static CameraRoom ActiveRoom
    {
        get
        {
            if (Instance == null) return null;

            List<CameraFramingZone> zones = Instance.activeZones;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                if (zones[i] is CameraRoom room && room != null) return room;
            }
            return null;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Only remove the duplicate component, never the GameObject it lives on.
            Destroy(this);
            return;
        }
        Instance = this;

        // Until a zone has been entered and left there is nothing to be symmetric with, so the
        // serialized default stands in.
        releaseSmoothTime = defaultTransitionTime;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Registers a zone the player has just walked into. Re-entering moves it back to the top of
    /// the stack, so a zone re-crossed after a partial exit still wins.
    /// </summary>
    public static void Enter(CameraFramingZone zone)
    {
        if (zone == null) return;

        CameraZoomController controller = EnsureInstance();
        controller.activeZones.Remove(zone);
        controller.activeZones.Add(zone);
    }

    /// <summary>Drops a zone the player has left. Falls back to whatever zone is beneath it.</summary>
    public static void Exit(CameraFramingZone zone)
    {
        if (Instance == null || zone == null) return;

        if (Instance.activeZones.Remove(zone))
        {
            // Ease back out over the time the zone itself asks for, so a room tuned to open slowly
            // closes just as slowly. Symmetry matters here: a room that takes a second to settle
            // into and a fifth of a second to leave reads as the camera being yanked at the door.
            Instance.releaseSmoothTime = zone.TransitionTime;
        }
    }

    private static CameraZoomController EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject host = new GameObject(nameof(CameraZoomController));
        // Awake runs inside AddComponent and assigns Instance; the return value is belt and braces.
        return host.AddComponent<CameraZoomController>();
    }

    private void Update()
    {
        if (!ResolveCamera()) return;

        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float targetSize = defaultSize;
        float smoothTime = releaseSmoothTime;

        // Last zone entered wins. A corridor zone overlapping a room at the doorway then resolves
        // to whichever the player most recently crossed into, rather than to whichever happens to
        // sit earlier in the list — and stepping back out falls to the one underneath.
        for (int i = activeZones.Count - 1; i >= 0; i--)
        {
            if (activeZones[i] == null)
            {
                activeZones.RemoveAt(i);
                continue;
            }

            targetSize = activeZones[i].GetTargetSize(defaultSize, aspect);
            smoothTime = activeZones[i].TransitionTime;
            break;
        }

        currentSmoothTime = smoothTime;
        currentSize = Mathf.SmoothDamp(currentSize, targetSize, ref sizeVelocity, smoothTime);
        vcam.Lens.OrthographicSize = currentSize;
    }

    /// <summary>
    /// Resolves the vcam lazily and latches the level's authored framing as the default. Reading it
    /// rather than hardcoding matters — the scenes disagree, 10.21 in Tutorial 1 against 8.06 in
    /// Level 1 — so zoom zones are authored as a multiplier and travel between scenes intact.
    /// </summary>
    private bool ResolveCamera()
    {
        if (resolved && vcam != null) return true;

        if (vcam == null) vcam = FindAnyObjectByType<CinemachineCamera>();
        if (vcam == null)
        {
            if (!warnedAboutMissingCamera)
            {
                DebugUtils.LogWarning(
                    "CameraZoomController found no CinemachineCamera in the scene — framing zones " +
                    "do nothing. Level 1's vcam is disabled, which is one way to hit this.");
                warnedAboutMissingCamera = true;
            }
            return false;
        }

        defaultSize = vcam.Lens.OrthographicSize;
        currentSize = defaultSize;
        sizeVelocity = 0f;

        // Rooms need the confiner to mean anything, so it comes along for the ride. CameraBounds
        // installs it the same way, so bounds work in a level with no rooms in it.
        CameraRoomConfiner.EnsureInstalled(vcam);

        resolved = true;
        return true;
    }
}
