using System;
using UnityEngine;

/// <summary>
/// One destructible weak spot on a boss. During the Shielded phase the boss itself is invulnerable
/// and these are the only things that can be hurt; clearing them all is what ends the phase.
///
/// Each one owns its own <see cref="Health"/>, so it takes damage through exactly the same
/// IDamageable path as any enemy — the player's slash and projectiles need no special case beyond
/// recognising the Weakpoint tag.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Collider2D))]
public class BossWeakpoint : MonoBehaviour
{
    /// <summary>Raised once, when this weakpoint is destroyed. <see cref="BossAI"/> counts these.</summary>
    public event Action<BossWeakpoint> Destroyed;

    [Tooltip("Hide the weakpoint until the Shielded phase begins. Leave ticked unless the art is " +
             "meant to be visible on the boss the whole time.")]
    [SerializeField] private bool hiddenUntilExposed = true;

    /// <summary>False once destroyed, so the boss can tell "all gone" from "none spawned yet".</summary>
    public bool IsAlive { get; private set; } = true;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
        health.Died += HandleDied;

        // Tagging matters as much as the component: Bullet decides what it may damage by tag, so
        // an untagged weakpoint silently ignores every projectile the player fires at it.
        if (!CompareTag("Weakpoint"))
        {
            DebugUtils.LogWarning(
                $"Weakpoint '{name}' isn't tagged Weakpoint, so projectiles will pass through it. " +
                "Set the tag in the Inspector.");
        }

        if (hiddenUntilExposed) SetExposed(false);
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= HandleDied;
    }

    /// <summary>
    /// Shows or hides the weakpoint as a phase starts and ends. Never called on the boss's own
    /// collider — that one has to stay live throughout so the player can keep hover-targeting it.
    /// </summary>
    public void SetExposed(bool exposed)
    {
        if (!IsAlive) return;

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true)) col.enabled = exposed;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = exposed;

        // Invulnerable rather than collider-off while hidden, as a second line of defence: an
        // area attack that finds the collider some other way still can't chip it early.
        if (health != null) health.Invulnerable = !exposed;
    }

    /// <summary>
    /// Brings a destroyed weakpoint back for a fresh attempt at the fight. Called by the boss when
    /// she resets, so a player who died after breaking two of three doesn't return to a phase that
    /// is already half over.
    ///
    /// Requires the weakpoint's Health to have Destroy On Death unticked — a destroyed GameObject
    /// has nothing left to revive.
    /// </summary>
    public void Revive()
    {
        IsAlive = true;

        if (health != null) health.ResetHealth();
        SetExposed(false);
    }

    private void HandleDied()
    {
        if (!IsAlive) return;
        IsAlive = false;

        DebugUtils.Log($"Weakpoint '{name}' destroyed.");
        Destroyed?.Invoke(this);

        // Left to the boss to decide what a cleared weakpoint looks like — it may want the husk
        // to stay on the model rather than vanish, so only the collider is guaranteed to go.
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true)) col.enabled = false;
    }
}
