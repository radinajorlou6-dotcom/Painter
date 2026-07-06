using UnityEngine;

public class Bullet : MonoBehaviour, IPoolable
{

    [SerializeField] private float damage = 10f;
    [SerializeField] private float speed = 20f;
    [SerializeField] private float timeTillDestroy;
    private Rigidbody2D rb;
    private ObjectPooling pool;
    private Coroutine destructionTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void AssignPool(ObjectPooling mainPool)
    {
        pool = mainPool;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        DebugUtils.Log("Bullet collided with: " + collision.gameObject.name);
        // Check if the bullet collides with an enemy
        if (collision.CompareTag("Player") || collision.CompareTag("Shield")) return;
        else if (collision.CompareTag("Enemy"))
        {
            // Apply damage to anything damageable we hit
            IDamageable target = collision.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }
        }
        if (pool != null)
        {
            pool.ReturnToPool(gameObject);
        }
        else
        {
            Destroy(gameObject); //Incase pooling doesnt work
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        if (pool != null)
        {
            if (destructionTimer != null) StopCoroutine(destructionTimer);
            destructionTimer = StartCoroutine(pool.ReturnToPoolWithDelay(gameObject, timeTillDestroy)); // Destroy the bullet after 5 seconds if it doesn't hit anything
        }
    }
}
