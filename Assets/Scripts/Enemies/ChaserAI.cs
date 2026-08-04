using System.Collections;
using UnityEngine;

/// <summary>
/// The simplest aggressive enemy: patrols until it spots the player, walks straight
/// at them, and swings once they're in range. Facing is handled by
/// EnemyBase.CheckPlayerDetection, which already turns us toward a visible player,
/// so "forward" (dirIsRight) is always the way we want to walk.
/// </summary>
public class ChaserAI : EnemyBase
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [Tooltip("Speed while chasing the player. Usually a little faster than the patrol speed.")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [Tooltip("Patrol back and forth when idle. Turn off for an enemy that guards one spot.")]
    [SerializeField] private bool patrolWhenIdle = true;

    [Header("Animation")]
    [SerializeField] private AnimationController animController;

    [Header("Attack")]
    [Tooltip("Empty child object marking where the swing lands, usually just in front of the body.")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.6f;
    [SerializeField] private float attackDmg = 10f;
    [SerializeField] private float attackKnockback = 8f;
    [SerializeField] private float knockbackDuration = 0.2f;
    [Tooltip("Telegraph before the hit lands, so the player has a moment to escape.")]
    [SerializeField] private float attackWindup = 0.25f;
    [Tooltip("Recovery after the hit, before the enemy can move again.")]
    [SerializeField] private float attackRecovery = 0.3f;
    [SerializeField] private float attackCooldown = 0.5f;

    private bool isAttacking = false;

    protected override void Awake()
    {
        base.Awake();
        if (animController == null) animController = GetComponent<AnimationController>();
    }

    protected override void Update()
    {
        base.Update(); // ground + wall checks

        // Committed to a swing, or reeling from a hit: no steering this frame.
        if (isAttacking || isBeingKnocked) return;

        if (canSeePlayer) Chase();
        else Patrol();
    }

    /// <summary>
    /// Close the distance, then swing. We only walk while there is floor ahead and
    /// nothing in the way — the chase should never march the enemy off a ledge.
    /// </summary>
    private void Chase()
    {
        if (currDistanceFromPlayer <= attackRange)
        {
            Stop();
            StartCoroutine(BaseAttack());
            return;
        }

        if (!HasFooting || !IsGroundAhead() || isColliding)
        {
            // Cornered or out of floor: hold position and wait for the player to come closer.
            Stop();
            return;
        }

        Walk(chaseSpeed);
    }

    /// <summary>Pace back and forth, turning around at ledges and walls.</summary>
    private void Patrol()
    {
        if (!patrolWhenIdle || !HasFooting)
        {
            Stop();
            return;
        }

        if (!IsGroundAhead() || isColliding)
        {
            Flip();
            Stop(); // not actually covering ground this frame
            return;
        }

        Walk(moveSpeed);
    }

    private void Walk(float speed)
    {
        float dir = dirIsRight ? 1f : -1f;
        // Setting linearVelocity directly is already per-second, so no Time.deltaTime here.
        rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        animController?.PlayAnimation(AnimationType.Walk);
    }

    private void Stop()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        animController?.PlayAnimation(AnimationType.Idle);
    }

    // IsGroundAhead lives on EnemyBase now — same probe for every walking enemy.

    /// <summary>
    /// Wind up, check what's in the strike zone on the active frame, then recover.
    /// The damage check happens after the windup rather than when the swing starts,
    /// so a player who dodges in time doesn't get hit.
    /// </summary>
    protected override IEnumerator BaseAttack()
    {
        isAttacking = true;

        animController?.PlayAnimation(AnimationType.Attack);
        yield return new WaitForSeconds(attackWindup);

        // The enemy may have died mid-windup; HandleDeath stops coroutines, but guard
        // anyway in case the object was torn down some other way.
        if (this == null || health.IsDead) yield break;

        Vector2 hitCenter = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
        Collider2D hit = Physics2D.OverlapCircle(hitCenter, attackRadius, playerLayer);
        if (hit != null) ApplyHit(hit);

        yield return new WaitForSeconds(attackRecovery);
        Stop();

        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

    /// <summary>
    /// Damage and shove whatever was caught, through the health interfaces so we
    /// never care about the concrete target type.
    /// </summary>
    private void ApplyHit(Collider2D hit)
    {
        IDamageable target = hit.GetComponent<IDamageable>();
        if (target != null) target.TakeDamage(attackDmg);

        IKnockbackable knockable = hit.GetComponent<IKnockbackable>();
        if (knockable != null)
        {
            Vector2 knockbackDir = (hit.transform.position - transform.position).normalized;
            StartCoroutine(knockable.TakeKnockback(knockbackDir, attackKnockback, knockbackDuration));
        }
    }

    // Spike death is handled by the Health component for every entity that has one.

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }

        // Where the enemy stops walking and starts swinging.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
