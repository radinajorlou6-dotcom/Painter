using UnityEngine;

/// <summary>
/// A wall-mounted button that breaks its wall when struck. Damage amount is ignored
/// on purpose — any connecting hit trips it. The collider must live on this same
/// GameObject, because attackers look the target up with GetComponent&lt;IDamageable&gt;()
/// on whatever collider they overlapped (see PlayerCombat.MeleeAttackRoutine).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ButtonScript : MonoBehaviour, IDamageable
{
    [Tooltip("The Health of the wall this button breaks.")]
    [SerializeField] private Health wallHealth;

    public void TakeDamage(float amount)
    {
        if (wallHealth == null)
        {
            DebugUtils.LogWarning(gameObject.name + " was hit but has no wall assigned.");
            return;
        }

        // Kill() ignores invulnerability and early-returns once the wall is already
        // dead, so hitting the button again after the wall is gone is harmless.
        wallHealth.Kill();
    }
}
