using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using System.Collections.Specialized;

public class BaseEnemyAI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float leapRange = 4f;
    [SerializeField] private Transform player;

    [Header("Movement")]
    [SerializeField] private float hopForce = 5f;
    [SerializeField] private float leapForce = 12f;
    [SerializeField] private float jumpInterval = 2f;
    [SerializeField] private float timeToTarget = 1f;

    [Header("Attack")]
    [SerializeField] private float damage = 34;

    private Rigidbody2D rb;
    private bool isJumping = false;
    private EnemyHealth healthScript; // To call Die() later

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        healthScript = GetComponent<EnemyHealth>();

        // Start the jumping loop
        StartCoroutine(JumpRoutine());
    }

    IEnumerator JumpRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(jumpInterval);

            float distance = Vector2.Distance(transform.position, player.position);

            if (distance <= detectionRange && !isJumping)
            {
                if (distance <= leapRange)
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

            healthScript.TakeDamage(999);
            
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
            healthScript.TakeDamage(999);
        }

        Debug.Log("Enemy collided with: " + collision.gameObject.name);
    }
}