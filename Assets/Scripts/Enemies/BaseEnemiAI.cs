using System.Collections.Generic;
using System.Collections;
using System.Collections.Specialized;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class BaseEnemyAI : EnemyHealth
{
    [Header("Movement")]
    [SerializeField] private float hopForce = 5f;
    [SerializeField] private float leapForce = 12f;
    [SerializeField] private float jumpInterval = 2f;
    [SerializeField] private float timeToTarget = 1f;

    [Header("Attack")]
    [SerializeField] private float damage = 34;

    private bool isJumping = false;

    protected override void Start()
    {
        // Start the jumping loop
        StartCoroutine(JumpRoutine());
    }

    protected override void Update()
    {
        GroundCheck();
        CollideCheck();
    }


    IEnumerator JumpRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(jumpInterval);
            if (!isGrounded) continue;
            if (isBeingKnocked) continue;

            float distance = Vector2.Distance(transform.position, player.position);

            if (distance <= detectionRange && !isJumping)
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
        float velocityY = deltaY / timeToTarget  - 0.5f * gravity * timeToTarget;

        //4. Apply the calculated velocity to the Rigidbody2D
        rb.linearVelocity = new Vector2(velocityX, velocityY);

    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Example inside an EnemyBullet.cs script
        if (collision.gameObject.CompareTag("Shield"))
        {
            // Find the PlayerCombat script and tell it the shield took a hit
            PlayerCombat cS = collision.gameObject.GetComponentInParent<PlayerCombat>();
            if(cS != null)
            {
                cS.TakeShieldHit();
            }

            Die();
            
        }

        else if (collision.gameObject.CompareTag("Player"))
        {
            // Do Damage to Player (You'll need a PlayerHealth script!)
            PlayerHealth ph = collision.gameObject.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(damage);
                Debug.Log("BOOM! Enemy exploded on player!");
            }
            // Suicide logic
            Die();
        }

        Debug.Log("Enemy collided with: " + collision.gameObject.name);
    }


}