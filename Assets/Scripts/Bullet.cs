using UnityEngine;

public class Bullet : MonoBehaviour, IPoolable
{
    /// <summary>
    /// Who fired it, which is the only thing that decides what it may hurt. Without this an enemy
    /// firing the same prefab would sail through the player and damage its own allies instead.
    /// </summary>
    public enum Owner
    {
        /// <summary>Hurts enemies and boss weakpoints. The original behaviour, so it stays default.</summary>
        Player,
        /// <summary>Hurts the player, and is stopped by their shield.</summary>
        Enemy
    }

    [Tooltip("Player projectiles hurt enemies; enemy projectiles hurt the player.")]
    [SerializeField] private Owner firedBy = Owner.Player;

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
        if (collision.CompareTag("Ignorables")) return; // Ignorables should never stop anything
        if (firedBy == Owner.Player)
        {
            // Never stopped by whoever fired it, or by their own shield.
            if (collision.CompareTag("Player") || collision.CompareTag("Shield")) return;

            // Weakpoint as well as Enemy: a shielded boss is invulnerable everywhere except its
            // weakpoints, so leaving them out here would make that phase unbeatable at range.
            if (collision.CompareTag("Enemy") || collision.CompareTag("Weakpoint"))
            {
                ApplyDamage(collision);
            }
        }
        else
        {
            // An enemy's own kind doesn't stop its shot, so it can fire past them.
            if (collision.CompareTag("Enemy") || collision.CompareTag("Weakpoint")) return;

            // The shield stops the shot without taking health damage — PlayerCombat owns what a
            // shield hit costs, via its own collision handling.
            if (collision.CompareTag("Player")) ApplyDamage(collision);
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
    /// <summary>
    /// Searches upward as well: a weakpoint's collider may sit on a child of the object that owns
    /// the Health, and a hit that finds nothing to damage would silently do nothing.
    /// </summary>
    private void ApplyDamage(Collider2D collision)
    {
        IDamageable target = collision.GetComponent<IDamageable>()
                             ?? collision.GetComponentInParent<IDamageable>();
        target?.TakeDamage(damage);
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
