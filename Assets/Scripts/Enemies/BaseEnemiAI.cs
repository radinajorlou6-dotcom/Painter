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
            if (!isGrounded) continue;      // never jump mid-air
            if (isBeingKnocked) continue;
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
        // Calculate direction only on X axis
        float direction = (player.position.x > transform.position.x) ? 1 : -1;
        rb.AddForce(new Vector2(direction * hopForce, hopForce), ForceMode2D.Impulse);
    }

    void LeapAttack()
    {
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

            health.Kill();
        }
    }
}
