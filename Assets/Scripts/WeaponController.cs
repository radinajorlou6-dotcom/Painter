using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the weapon pivot. By default the weapon continuously aims at the mouse.
/// While the player is building a slash the weapon "waits" (holds its angle), and
/// once the slash is released it sweeps along the measured swing arc before handing
/// control back to normal aiming.
///
/// This only rotates a pivot; it doesn't decide when a slash happens. The combat
/// scripts call <see cref="BeginHold"/>, <see cref="ResumeAim"/> and
/// <see cref="PlaySlashFollow"/> at the right moments.
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Tooltip("The transform that actually rotates. Defaults to this object if left empty.")]
    [SerializeField] private Transform pivot;

    [Tooltip("Added to the computed angle so the sprite lines up. Use 0 if the weapon art " +
             "points along +X at rest, -90 if it points up, etc.")]
    [SerializeField] private float angleOffset = 0f;

    private Camera cam;

    private enum WeaponState { Aiming, Holding, Following }
    private WeaponState state = WeaponState.Aiming;
    private Coroutine followRoutine;

    private void Awake()
    {
        if (pivot == null) pivot = transform;
        cam = Camera.main;
    }

    private void Update()
    {
        // Only chase the mouse while aiming. Holding freezes the angle; Following is
        // driven by the sweep coroutine.
        if (state == WeaponState.Aiming)
        {
            AimAtMouse();
        }
    }

    private void AimAtMouse()
    {
        if (cam == null || Mouse.current == null) return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 direction = mouseWorld - (Vector2)pivot.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        SetAngle(angle);
    }

    private void SetAngle(float angleDeg)
    {
        pivot.rotation = Quaternion.Euler(0f, 0f, angleDeg + angleOffset);
    }

    /// <summary>Freeze the weapon at its current angle while the player builds a slash.</summary>
    public void BeginHold()
    {
        StopFollow();
        state = WeaponState.Holding;
    }

    /// <summary>Return to aiming at the mouse (used when a gesture doesn't become a slash).</summary>
    public void ResumeAim()
    {
        StopFollow();
        state = WeaponState.Aiming;
    }

    /// <summary>
    /// Sweep the weapon from the slash's start angle to its final angle over the given
    /// duration, then resume aiming. Angles come straight from SlashGeometry.MeasureSwing,
    /// so the weapon traces the exact path the hitbox used.
    /// </summary>
    public void PlaySlashFollow(float startAngle, float finalAngle, float duration)
    {
        StopFollow();
        followRoutine = StartCoroutine(FollowRoutine(startAngle, finalAngle, duration));
    }

    private IEnumerator FollowRoutine(float startAngle, float finalAngle, float duration)
    {
        state = WeaponState.Following;

        if (duration <= 0f) duration = 0.0001f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Plain Lerp (not LerpAngle): start/final already encode swing direction and
            // magnitude, so we want the literal path, not the shortest rotation.
            float angle = Mathf.Lerp(startAngle, finalAngle, elapsed / duration);
            SetAngle(angle);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetAngle(finalAngle);
        state = WeaponState.Aiming;
        followRoutine = null;
    }

    private void StopFollow()
    {
        if (followRoutine != null)
        {
            StopCoroutine(followRoutine);
            followRoutine = null;
        }
    }
}
