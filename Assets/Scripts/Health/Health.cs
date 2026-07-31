using System;
using UnityEngine;

/// <summary>
/// Reusable health component. Attach it to any entity that needs HP (player,
/// enemies, destructible props). It owns health state and broadcasts changes
/// through events, keeping visuals, AI and game-state logic decoupled
/// (single responsibility). Composition like this replaces the old habit of
/// inheriting from a shared "Health" base class.
///
/// Optional destroyOnDeath handles the common case of a prop that just needs to
/// disappear; anything with real death logic ignores it and listens to Died.
/// </summary>
public class Health : MonoBehaviour, IHealth
{
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("When true the entity ignores all incoming TakeDamage calls " +
             "(e.g. armoured enemies). Kill() still works so hazards can force death.")]
    [SerializeField] private bool invulnerable = false;

    [Header("Death")]
    [Tooltip("Remove this object from the scene once health hits zero. Leave off for " +
             "entities that clean themselves up (the player, enemies via EnemyBase); " +
             "turn it on for props like destructible walls that need no death logic.")]
    [SerializeField] private bool destroyOnDeath = false;

    [Tooltip("Seconds to wait before destroying, so a death animation or effect can play.")]
    [SerializeField] private float destroyDelay = 0f;

    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0f;

    /// <summary>Toggle at runtime for temporary invulnerability (i-frames, armour).</summary>
    public bool Invulnerable { get => invulnerable; set => invulnerable = value; }

    public event Action<float, float> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || invulnerable || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        // Centralized camera shake based on damage done or suffered
        if (CameraShake.Instance != null)
        {
            if (gameObject.CompareTag("Player"))
            {
                // Suffer damage: slightly stronger shake
                float intensity = 0.15f + Mathf.Clamp(amount * 0.005f, 0f, 0.15f);
                float duration = 0.15f + Mathf.Clamp(amount * 0.002f, 0f, 0.10f);
                CameraShake.Instance.Shake(intensity, duration);
            }
            else
            {
                // Deal damage: satisfying feedback
                float intensity = 0.10f + Mathf.Clamp(amount * 0.003f, 0f, 0.10f);
                float duration = 0.10f + Mathf.Clamp(amount * 0.002f, 0f, 0.08f);
                CameraShake.Instance.Shake(intensity, duration);
            }
        }

        if (IsDead)
        {
            RaiseDeath();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Kill()
    {
        if (IsDead) return;

        currentHealth = 0f;
        HealthChanged?.Invoke(currentHealth, maxHealth);
        RaiseDeath();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Single exit point for death. Listeners (EnemyBase, PlayerHealth, UI) always run
    /// first so they can play animations or update state, then the object cleans itself
    /// up if it was told to.
    /// </summary>
    private void RaiseDeath()
    {
        Died?.Invoke();

        if (destroyOnDeath) DestroySelf();
    }

    /// <summary>
    /// Remove this object from the scene. Called automatically on death when
    /// destroyOnDeath is ticked, and public so a death animation event or another
    /// script can trigger the cleanup at exactly the right moment instead.
    /// </summary>
    public void DestroySelf()
    {
        // Stop the corpse blocking or damaging anything while a delayed animation plays.
        foreach (Collider2D col in GetComponents<Collider2D>()) col.enabled = false;

        DebugUtils.Log(gameObject.name + " destroyed by Health.");
        Destroy(gameObject, destroyDelay);
    }
}
