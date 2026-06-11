using System.Collections.Specialized;
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
        Debug.Log("Bullet collided with: " + collision.gameObject.name);
        // Check if the bullet collides with an enemy
        if (collision.CompareTag("Player") || collision.CompareTag("Shield")) return;
        else if (collision.CompareTag("Enemy"))
        {
            // Apply damage to the enemy
            EnemyHealth enemyHealth = collision.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
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

    // Update is called once per frame
    void Update()
    {
        
    }
}
