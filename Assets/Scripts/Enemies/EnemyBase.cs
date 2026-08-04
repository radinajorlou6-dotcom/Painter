using System.Collections;
using UnityEngine;

/// <summary>
/// Shared behaviour for every enemy: player detection, ground/wall checks,
/// knockback reaction and death handling. Health is composed via a required
/// <see cref="Health"/> component (IHealth) rather than inherited, so all
/// damage logic lives in one reusable place and subclasses only add AI.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour, IKnockbackable
{
    [Header("Detection")]
    [SerializeField] protected float detectionRange = 10f;
    [SerializeField] protected float attackRange = 4f;
    [SerializeField] protected Transform player;
    protected float currDistanceFromPlayer;
    protected bool canSeePlayer = false;

    protected bool isBeingKnocked = false;
    protected Rigidbody2D rb;
    protected bool dirIsRight = true;

    //Ground check variables
    [Header("GroundCheck")]
    [SerializeField] protected Transform groundCheck;
    [SerializeField] protected Transform collideCheck;
    [SerializeField] protected Vector2 groundCheckSize = new Vector2(0.5f, 0.05f);
    [SerializeField] protected Vector2 collideCheckSize = new Vector2(0.05f, 0.5f);
    [SerializeField] protected LayerMask groundLayer;
    [SerializeField] protected LayerMask collideWithLayer;
    [SerializeField] protected LayerMask environment;
    [SerializeField] protected LayerMask playerLayer;
    [Tooltip("Grace period after the ground check fails before the enemy counts as airborne. Absorbs the one-frame flickers you get on slopes, tile seams and landings.")]
    [SerializeField] protected float groundedGrace = 0.15f;
    public bool isGrounded { get; private set; } = true;
    protected bool isColliding = false;

    /// <summary>Time.time of the last frame the ground check succeeded.</summary>
    private float lastGroundedTime = float.NegativeInfinity;

    /// <summary>
    /// Grounded, or grounded recently enough that a single flickered frame shouldn't change
    /// behaviour. Steering logic should use this rather than isGrounded, so a momentary blip
    /// can't skip a ledge check.
    /// </summary>
    protected bool HasFooting => isGrounded || Time.time - lastGroundedTime <= groundedGrace;

    [Header("Ledge Detection")]
    [Tooltip("How far ahead of the body to look for floor before taking a step.")]
    [SerializeField] protected float edgeCheckDistance = 0.5f;
    [Tooltip("How far below the feet that look reaches. Deeper than any step the enemy may walk down, shallower than a drop it should refuse.")]
    [SerializeField] protected float edgeCheckDepth = 0.75f;

    [Header("Death")]
    [SerializeField] protected Animator anim;
    [Tooltip("Seconds to wait for the death animation before the object is destroyed.")]
    [SerializeField] protected float deathAnimationDuration = 0.8f;

    /// <summary>The composed health component. Subclasses use it to deal/take damage and check death.</summary>
    protected Health health;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        health.Died += HandleDeath;
        StartCoroutine(DetectionRoutine());
    }

    protected virtual void OnDestroy()
    {
        // Always unsubscribe to avoid dangling handlers on destroyed objects.
        if (health != null) health.Died -= HandleDeath;
    }

    protected virtual void Update()
    {
        GroundCheck();
        CollideCheck();
    }

    protected IEnumerator DetectionRoutine()
    {
        while (true)
        {
            CheckPlayerDetection();
            float interval = currDistanceFromPlayer / 100; //Run based on how far the player is
            yield return new WaitForSeconds(Mathf.Max(0.05f, interval));
        }
    }

    protected abstract IEnumerator BaseAttack();

    protected virtual void CheckPlayerDetection()
    {
        if (player == null) return;
        currDistanceFromPlayer = Vector2.Distance(transform.position, player.position); //Check how far the player is
        RaycastHit2D seePlayer = Physics2D.Linecast(transform.position, player.position, environment); //Check if theres anything in the way
        if (currDistanceFromPlayer <= detectionRange && seePlayer.collider == null) //if player is within detection range and theres nothing in the way
        {
            // Determine if the player is to the left or right of us
            bool playerIsRight = player.position.x > transform.position.x;

            // If player is to the right but we are facing left
            if (playerIsRight && !dirIsRight)
            {
                Flip();
            }
            // If player is to the left but we are facing right
            else if (!playerIsRight && dirIsRight)
            {
                Flip();
            }

            canSeePlayer = true;
        }
        else
        {
            canSeePlayer = false;
        }
    }

    public virtual IEnumerator TakeKnockback(Vector2 direction, float force, float duration)
    {
        if (rb == null) yield break;
        rb.linearVelocity = Vector2.zero;

        isBeingKnocked = true;
        Vector2 initialVelocity = direction.normalized * force;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (this == null || rb == null) yield break;

            // t goes 0 -> 1 over the duration, velocity goes full -> zero
            float t = elapsed / duration;
            float smoothT = t * t;
            rb.linearVelocity = Vector2.Lerp(initialVelocity, Vector2.zero, smoothT);

            elapsed += Time.deltaTime;
            yield return null; // wait one frame
        }

        if (this == null || rb == null) yield break;

        rb.linearVelocity = Vector2.zero;
        isBeingKnocked = false;
    }

    /// <summary>
    /// Runs when the Health component reports death. Stops AI, freezes physics
    /// so gravity can't drag the body around mid-animation, plays the death
    /// animation, then destroys the object once the animation has had time to finish.
    /// </summary>
    protected virtual void HandleDeath()
    {
        StopAllCoroutines();
        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;

        if (anim != null) anim.SetTrigger("Died");

        DebugUtils.Log(gameObject.name + " has died.");
        Destroy(gameObject, deathAnimationDuration);
    }

    /// <summary>
    /// Optional animation-event hook: lets the death clip destroy the object on
    /// its final frame instead of relying on the timed fallback in HandleDeath.
    /// </summary>
    public void DieDestroy()
    {
        
        this.enabled = false;
        Destroy(gameObject);
    }

    protected virtual void GroundCheck()
    {
        isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0, groundLayer);
        if (isGrounded) lastGroundedTime = Time.time;
    }

    /// <summary>
    /// True if there is floor ahead to step onto. Casts DOWN from a point in front of the feet
    /// rather than testing a box at exactly foot height: groundCheckSize is only a few centimetres
    /// tall, so a box there reported "no ground" on any slope, step or landing bounce.
    /// </summary>
    protected bool IsGroundAhead()
    {
        Vector2 origin = new Vector2(
            transform.position.x + (dirIsRight ? edgeCheckDistance : -edgeCheckDistance),
            groundCheck.position.y);
        return Physics2D.Raycast(origin, Vector2.down, edgeCheckDepth, groundLayer);
    }

    protected virtual void CollideCheck()
    {
        isColliding = Physics2D.OverlapBox(collideCheck.position, collideCheckSize, 0, collideWithLayer);
    }

    protected virtual void Flip()
    {
        dirIsRight = !dirIsRight;
        transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
        if (collideCheck != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(collideCheck.position, collideCheckSize);
        }
        if (groundCheck != null)
        {
            // The ledge probe, so edgeCheckDistance/Depth can be tuned by eye.
            Gizmos.color = Color.yellow;
            Vector3 origin = new Vector3(
                transform.position.x + (dirIsRight ? edgeCheckDistance : -edgeCheckDistance),
                groundCheck.position.y,
                transform.position.z);
            Gizmos.DrawLine(origin, origin + Vector3.down * edgeCheckDepth);
        }
    }
}
