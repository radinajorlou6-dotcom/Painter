using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives every <see cref="ParallaxLayer"/> in the scene from one place.
///
/// Layers could each run their own LateUpdate, but three things make a single driver worth it:
///
///   1. Ordering. The CinemachineBrain on the Main Camera blends in LateUpdate, so parallax has to
///      read the camera *after* it. [DefaultExecutionOrder(1000)] settles that once here instead of
///      being a footgun on every layer.
///   2. The first frame. The CinemachinePositionComposer has Center On Activate enabled, so the
///      camera snaps onto the player during the first LateUpdate. A layer that recorded where the
///      camera "started" in Awake or Start would record the pre-snap position and be permanently
///      offset by the size of that snap. Layers therefore latch their origin on their first driven
///      frame, once the brain has already run.
///   3. CameraShake writes its offset at the Finalize stage of the Cinemachine pipeline, so reading
///      the camera transform this late includes shake. Distant layers then shake proportionally
///      less than the foreground, which is what real parallax does — for free.
///
/// Nothing needs to place this in a scene: the first layer to enable itself creates one.
/// </summary>
[DefaultExecutionOrder(1000)]
public class ParallaxController : MonoBehaviour
{
    public static ParallaxController Instance { get; private set; }

    [Header("Debug")]
    [Tooltip("Turn every parallax layer off at once. Layers freeze where they are rather than " +
             "snapping home, so this is safe to toggle while playing.")]
    [SerializeField] private bool parallaxEnabled = true;

    private readonly List<ParallaxLayer> layers = new List<ParallaxLayer>();
    private Camera trackedCamera;
    private bool warnedAboutMissingCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Only remove the duplicate component, never the GameObject it lives on — it may be
            // something the level cares about.
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Adds a layer to the driven set, creating the controller if this is the first one.
    /// Called from <see cref="ParallaxLayer.OnEnable"/>.
    /// </summary>
    public static void Register(ParallaxLayer layer)
    {
        if (layer == null) return;

        ParallaxController controller = EnsureInstance();
        if (!controller.layers.Contains(layer))
        {
            controller.layers.Add(layer);
        }
    }

    /// <summary>Removes a layer from the driven set. Called from <see cref="ParallaxLayer.OnDisable"/>.</summary>
    public static void Unregister(ParallaxLayer layer)
    {
        if (Instance == null || layer == null) return;
        Instance.layers.Remove(layer);
    }

    /// <summary>
    /// The controller is a convenience, not something a level designer should have to remember, so
    /// it builds itself on demand. The GameObject is scene-local and dies with the scene, which is
    /// what we want — layers are per-scene too.
    /// </summary>
    private static ParallaxController EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameObject host = new GameObject(nameof(ParallaxController));
        // Awake runs inside AddComponent and assigns Instance; the return value is belt and braces.
        return host.AddComponent<ParallaxController>();
    }

    private void LateUpdate()
    {
        if (!parallaxEnabled || layers.Count == 0) return;
        if (!ResolveCamera()) return;

        Vector3 cameraPosition = trackedCamera.transform.position;

        for (int i = layers.Count - 1; i >= 0; i--)
        {
            ParallaxLayer layer = layers[i];
            if (layer == null)
            {
                // A layer destroyed without OnDisable firing (scene teardown) leaves a hole.
                layers.RemoveAt(i);
                continue;
            }

            // Layers latch on their own first driven frame rather than all at once, so one spawned
            // mid-level starts from where the camera is now instead of inheriting a stale origin.
            if (!layer.HasOrigin) layer.CaptureOrigin(cameraPosition);

            layer.ApplyParallax(cameraPosition);
        }
    }

    /// <summary>
    /// Resolves the camera lazily and re-resolves if it goes away, so the controller survives a
    /// scene transition that swaps the camera out from under it.
    /// </summary>
    private bool ResolveCamera()
    {
        if (trackedCamera != null) return true;

        trackedCamera = Camera.main;
        if (trackedCamera == null)
        {
            if (!warnedAboutMissingCamera)
            {
                DebugUtils.LogWarning(
                    "ParallaxController found no camera tagged MainCamera — parallax is idle. " +
                    "Tag the scene's camera 'MainCamera'.");
                warnedAboutMissingCamera = true;
            }
            return false;
        }

        warnedAboutMissingCamera = false;
        return true;
    }
}
