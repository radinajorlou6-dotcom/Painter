using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Anything that can receive damage. Attackers (bullets, slashes, enemies)
/// depend on this abstraction instead of concrete health classes, so they
/// don't care whether they hit the player, an enemy, or a destructible prop.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}

/// <summary>
/// Full health contract. Tracks HP, exposes read-only state, and raises
/// events so unrelated systems (UI, AI, game state) can react without being
/// tightly coupled to the entity that owns the health.
/// </summary>
public interface IHealth : IDamageable
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsDead { get; }

    /// <summary>Raised whenever health changes. Args: (current, max).</summary>
    event Action<float, float> HealthChanged;

    /// <summary>Raised once, the moment health reaches zero.</summary>
    event Action Died;

    void Heal(float amount);

    /// <summary>Force health to zero regardless of invulnerability (e.g. spikes).</summary>
    void Kill();

    /// <summary>Restore to full health. Useful for respawns and pooling.</summary>
    void ResetHealth();
}

/// <summary>
/// Anything that can be knocked back. The physics reaction is entity-specific
/// (the player disables movement, an enemy sets a knocked flag) so the
/// implementation stays on the entity while callers depend only on this.
/// </summary>
public interface IKnockbackable
{
    IEnumerator TakeKnockback(Vector2 direction, float force, float duration);
}
