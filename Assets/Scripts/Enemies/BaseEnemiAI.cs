using System.Collections;
using UnityEngine;

/// <summary>
/// A "leaper" enemy: hops toward the player and explodes on impact. Health and
/// death are handled by the composed Health component on the same GameObject.
/// </summary>
public class BaseEnemyAI : EnemyBase
{
    [Header("Movement")]
    [SerializeField] private float hopForce = 5f;
    [SerializeField] private float leapForce = 12f;
    [SerializeField] private float jumpInterval = 2f;
    [SerializeField] private float timeToTarget = 1f;

    [Header("Attack")]
    [SerializeField] private float damage = 34;

    [Header("Explosion Feedback")]
    [Tooltip("Freeze-frame length when the enemy detonates on the player.")]
    [SerializeField] private float explosionHitStop = 0.08f;
    [Tooltip("Camera shake intensity/duration on detonation.")]
    [SerializeField] private float explosionShake = 0.35f;
    [SerializeField] private float explosionShakeDuration = 0.25f;

    [Header("Wall Sticking")]
    [Tooltip("How horizontal a contact normal must be to count as a wall (1 = perfectly vertical wall, 0 = flat floor).")]
    [SerializeField] private float wallNormalThreshold = 0.5f;

    private bool isStuckToWall = false;
    private float defaultGravityScale;

    protected override void Awake()
    {
        base.Awake();
        // Remember the normal gravity so we can restore it after unsticking.
        defaultGravityScale = rb.gravityScale;
    }

    private void Start()
    {
        // Start the jumping loop
        StartCoroutine(JumpRoutine());
    }

    IEnumerator JumpRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(jumpInterval);
            // Attack whenever we have a foothold: on the ground OR stuck to a wall.
            if (!isGrounded && !isStuckToWall) continue;
            if (isBeingKnocked) continue;
            // Cursed: skip this beat rather than being guarded at the top, because this enemy is
            // driven by the coroutine rather than Update. Wall-stick state is left alone — the
            // stun freezes position anyway, so gravityScale stays whatever Unstick will restore.
            if (IsStunned) continue;
            if (player == null) continue;

            float distance = Vector2.Distance(transform.position, player.position);

            if (distance <= detectionRange)
            {
                if (distance <= attackRange)
                {
                    LeapAttack();
                }
                else
                {
                    SmallHop();
                }
            }
        }
    }

    protected override IEnumerator BaseAttack()
    {
        //do nothing
        yield break;
    }

    void SmallHop()
    {
        Unstick(); // release from any wall before pushing off

        // Calculate direction only on X axis
        float direction = (player.position.x > transform.position.x) ? 1 : -1;
        rb.AddForce(new Vector2(direction * hopForce, hopForce), ForceMode2D.Impulse);
    }

    void LeapAttack()
    {
        Unstick(); // release (and restore gravity) BEFORE the arc math reads gravityScale

        Vector2 startPos = transform.position; // Starting position of the leap
        Vector2 targetPos = player.position; // Target position of the leap

        // Calculate the initial velocity needed to reach the target in the specified time

        //1. Calculate the distance to the target
        float deltaX = targetPos.x - startPos.x;
        float deltaY = targetPos.y - startPos.y;

        //2. Get the games gravity
        float gravity = Physics2D.gravity.y * rb.gravityScale;

        //3. The equations
        float velocityX = deltaX / timeToTarget;
        float velocityY = deltaY / timeToTarget - 0.5f * gravity * timeToTarget;

        //4. Apply the calculated velocity to the Rigidbody2D
        rb.linearVelocity = new Vector2(velocityX, velocityY);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // A dead hopper has already detonated. Without this it can still damage the player on the
        // way out — a contact resolved in the same physics step as the killing blow lands here
        // after Health has already raised Died.
        if (health.IsDead) return;

        if (collision.gameObject.CompareTag("Shield"))
        {
            // Tell the PlayerCombat script the shield absorbed a hit, then detonate.
            PlayerCombat cS = collision.gameObject.GetComponentInParent<PlayerCombat>();
            if (cS != null)
            {
                cS.TakeShieldHit();
            }

            health.Kill();
        }
        else if (collision.gameObject.CompareTag("Player"))
        {
            // Deal damage to whatever can be damaged on the player, then detonate.
            IDamageable target = collision.gameObject.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(damage);
                DebugUtils.Log("BOOM! Enemy exploded on player!");
            }

            // Explosion emphasis: bigger than a normal hit, and Kill() itself is silent.
            HitStop.Instance?.Freeze(explosionHitStop);
            CameraShake.Instance?.Shake(explosionShake, explosionShakeDuration);

            health.Kill();
        }
        // If we hit a wall while airborne, cling to it and keep attacking from there.
        else if (!isGrounded && IsWallContact(collision, out Vector2 wallNormal))
        {
            StickToWall(wallNormal);
        }
    }

    /// <summary>
    /// A contact counts as a wall if it's on the collide layer AND the surface
    /// normal is roughly horizontal (so we ignore floors and ceilings). Outputs
    /// the wall's surface normal so we can orient the body against it.
    /// </summary>
    private bool IsWallContact(Collision2D collision, out Vector2 normal)
    {
        normal = Vector2.up;
        if (((1 << collision.gameObject.layer) & collideWithLayer) == 0) return false;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.x) > wallNormalThreshold)
            {
                normal = contact.normal;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Freeze on the wall: kill motion and gravity so the enemy hangs in place,
    /// and rotate the body so its "feet" rest against the wall. Because the wall
    /// normal points straight out of the surface, aligning local up with it makes
    /// the blob look like it's sitting on the wall just as it sits on the ground.
    /// </summary>
    private void StickToWall(Vector2 wallNormal)
    {
        isStuckToWall = true;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        transform.up = wallNormal;
    }

    /// <summary>Release from the wall, restore gravity, and stand upright again.</summary>
    private void Unstick()
    {
        if (!isStuckToWall) return;
        isStuckToWall = false;
        rb.gravityScale = defaultGravityScale;
        transform.up = Vector3.up;
    }
}
