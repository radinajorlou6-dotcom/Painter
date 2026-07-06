using System;
using UnityEngine;

/// <summary>
/// Reusable health component. Attach it to any entity that needs HP (player,
/// enemies, destructible props). It owns only health state and broadcasts
/// changes through events, keeping visuals, AI and game-state logic decoupled
/// (single responsibility). Composition like this replaces the old habit of
/// inheriting from a shared "Health" base class.
/// </summary>
public class Health : MonoBehaviour, IHealth
{
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("When true the entity ignores all incoming TakeDamage calls " +
             "(e.g. armoured enemies). Kill() still works so hazards can force death.")]
    [SerializeField] private bool invulnerable = false;

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

        if (IsDead)
        {
            Died?.Invoke();
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
        Died?.Invoke();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
