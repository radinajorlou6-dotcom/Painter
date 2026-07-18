using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the weapon pivot. The weapon sits at a fixed rest pose and does NOT track the
/// mouse during normal movement. It only moves while an action is happening:
///   • Slash / stab  -> sweeps along the swing arc, then returns to rest.
///   • Shield / platform draw -> follows the mouse while drawing, then returns to rest.
///   • Projectile     -> snaps toward the shot, holds briefly, then returns to rest.
///
/// Every action ends by rotating back to the pose it started from. This class only rotates
/// a pivot; the combat scripts decide when each action begins and ends.
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Tooltip("The transform that actually rotates. Defaults to this object if left empty.")]
    [SerializeField] private Transform pivot;

    [Tooltip("The weapon sprite. Hidden while at rest, shown during any action. " +
             "Auto-found in children if left empty.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Added to the computed aim angle so the sprite lines up. 0 if the art points " +
             "along +X at rest, -90 if it points up, etc.")]
    [SerializeField] private float angleOffset = 0f;

    [Tooltip("Seconds to rotate back to the rest pose after an action. 0 = instant.")]
    [SerializeField] private float returnDuration = 0.15f;

    [Tooltip("How long the weapon points toward a projectile shot before returning to rest.")]
    [SerializeField] private float projectileHoldTime = 0.1f;

    [Tooltip("If the cursor is closer than this to the pivot, keep the current angle instead of " +
             "recomputing. Stops wild spinning when you draw right on top of the player.")]
    [SerializeField] private float minAimDistance = 0.5f;

    private Camera cam;
    private float restAngle;          // world Z the weapon rests at (captured on Awake)
    private bool followMouse = false; // true only while drawing a shield/platform
    private Coroutine actionRoutine;

    private void Awake()
    {
        if (pivot == null) pivot = transform;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        cam = Camera.main;
        restAngle = pivot.eulerAngles.z; // the pose everything returns to
        SetVisible(false);               // starts at rest, so hidden
    }

    private void Update()
    {
        // The only continuous-tracking case: actively drawing a shield or platform.
        if (followMouse) AimAtMouse();
    }

    #region Public API (called by the combat scripts)

    /// <summary>Slash wind-up: snap to where the slash begins and hold there until release.</summary>
    public void BeginHold()
    {
        StopAction();
        followMouse = false;
        SetVisible(true);
        AimAtMouse(); // freeze pointing at the start of the swing
    }

    /// <summary>
    /// Sweep the weapon from the slash's start angle to its final angle over the duration,
    /// then rotate back to the rest pose. Angles come from SlashGeometry.MeasureSwing.
    /// </summary>
    public void PlaySlashFollow(float startAngle, float finalAngle, float duration)
    {
        StopAction();
        followMouse = false;
        SetVisible(true);
        actionRoutine = StartCoroutine(SweepThenReturn(startAngle, finalAngle, duration));
    }

    /// <summary>Begin following the mouse while a shield/platform is being drawn.</summary>
    public void BeginMouseFollow()
    {
        StopAction();
        followMouse = true;
        SetVisible(true);
    }

    /// <summary>Point at the shot for a beat, then return to rest (projectile feedback).</summary>
    public void PointOnce(Vector2 worldTarget)
    {
        StopAction();
        followMouse = false;
        SetVisible(true);
        actionRoutine = StartCoroutine(PointThenReturn(worldTarget, projectileHoldTime));
    }

    /// <summary>Stop whatever the weapon is doing and rotate back to the rest pose.</summary>
    public void ReturnToRest()
    {
        StopAction();
        followMouse = false;
        actionRoutine = StartCoroutine(ReturnOnly());
    }

    #endregion

    #region Rotation helpers

    private void AimAtMouse()
    {
        if (cam == null || Mouse.current == null) return;
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        // Near the pivot the direction vector is tiny and its angle swings wildly with the
        // smallest cursor movement, which reads as erratic spinning. Hold the last angle instead.
        Vector2 direction = mouseWorld - (Vector2)pivot.position;
        if (direction.sqrMagnitude < minAimDistance * minAimDistance) return;

        SetAngle(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private float AngleTo(Vector2 worldTarget)
    {
        Vector2 direction = worldTarget - (Vector2)pivot.position;
        if (direction.sqrMagnitude < 0.0001f) return pivot.eulerAngles.z - angleOffset;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private void SetAngle(float directionAngle)
    {
        pivot.rotation = Quaternion.Euler(0f, 0f, directionAngle + angleOffset);
    }

    #endregion

    #region Coroutines

    private IEnumerator SweepThenReturn(float startAngle, float finalAngle, float duration)
    {
        yield return Sweep(startAngle, finalAngle, duration);
        yield return ReturnLerp();
        actionRoutine = null;
    }

    private IEnumerator PointThenReturn(Vector2 worldTarget, float hold)
    {
        SetAngle(AngleTo(worldTarget));
        if (hold > 0f) yield return new WaitForSeconds(hold);
        yield return ReturnLerp();
        actionRoutine = null;
    }

    private IEnumerator ReturnOnly()
    {
        yield return ReturnLerp();
        actionRoutine = null;
    }

    private IEnumerator Sweep(float startAngle, float finalAngle, float duration)
    {
        if (duration <= 0f) duration = 0.0001f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Plain Lerp (not LerpAngle): start/final already encode direction and magnitude.
            SetAngle(Mathf.Lerp(startAngle, finalAngle, elapsed / duration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        SetAngle(finalAngle);
    }

    private IEnumerator ReturnLerp()
    {
        float start = pivot.eulerAngles.z;

        if (returnDuration > 0f)
        {
            float t = 0f;
            while (t < returnDuration)
            {
                // LerpAngle here so it takes the shortest path back to rest.
                float z = Mathf.LerpAngle(start, restAngle, t / returnDuration);
                pivot.rotation = Quaternion.Euler(0f, 0f, z);
                t += Time.deltaTime;
                yield return null;
            }
        }

        pivot.rotation = Quaternion.Euler(0f, 0f, restAngle);
        SetVisible(false); // fully back at rest, hide the weapon again
    }

    private void SetVisible(bool visible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
    }

    private void StopAction()
    {
        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }
    }

    #endregion
}
