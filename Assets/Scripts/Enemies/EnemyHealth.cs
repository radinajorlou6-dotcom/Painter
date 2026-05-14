using System.Collections;
using TMPro.EditorUtilities;
using UnityEngine;

public abstract class EnemyHealth : MonoBehaviour
{

    [Header("Detection")]
    [SerializeField] protected float detectionRange = 10f;
    [SerializeField] protected float attackRange = 4f;
    [SerializeField] protected Transform player;

    [SerializeField] protected float maxHealth = 100f;
    protected float health;
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
    protected bool isGrounded = true;
    protected bool isColliding = false;

    protected virtual void Awake()
    {
        health = maxHealth; // Initialize health to maxHealth at the start
        rb = GetComponent<Rigidbody2D>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        GroundCheck();
        CollideCheck();
    }

    

    public virtual void TakeDamage(float damage)
    {
        health -= damage;
        Debug.Log(gameObject.name + " took " + damage + " damage. Remaining health: " + health);
        if (health <= 0)
        {
            Die();
        }
    }

    public virtual IEnumerator TakeKnockback(Vector2 attackDir, float knockbackMult, float knockbackDur)
    {
        if (rb == null) yield break;

        isBeingKnocked = true;
        Vector2 initialVelocity = attackDir.normalized * knockbackMult;
        float elapsed = 0f;

        while (elapsed < knockbackDur)
        {
            if (this == null || rb == null) yield break;

            // t goes 0 -> 1 over the duration, velocity goes full -> zero
            float t = elapsed / knockbackDur;
            float smoothT = t * t;
            rb.linearVelocity = Vector2.Lerp(initialVelocity, Vector2.zero, smoothT);

            elapsed += Time.deltaTime;
            yield return null; // wait one frame
        }

        if (this == null || rb == null) yield break;

        rb.linearVelocity = Vector2.zero;
        isBeingKnocked = false;
    }

    public virtual void Die()
    {
        Debug.Log(gameObject.name + " has died.");
        Destroy(gameObject);
    }

    protected virtual void GroundCheck()
    {
        if (Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0, groundLayer))
        {
            Debug.Log("Grounded reached");
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    protected virtual void CollideCheck()
    {
        if (Physics2D.OverlapBox(collideCheck.position, collideCheckSize, 0, collideWithLayer))
        {
            Debug.Log("Colliding with");
            isColliding = true;
        }
        else
        {
            isColliding= false;
        }
    }

    protected virtual void PlayerInSight()
    {

    }

    protected virtual void Flip()
    {
        dirIsRight = !dirIsRight;
        transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(collideCheck.position, collideCheckSize);
    }
}
