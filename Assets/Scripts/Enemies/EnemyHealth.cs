using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    public float health;
    public bool isBeingKnocked = false;
    private Rigidbody2D rb;

    private void Awake()
    {
        health = maxHealth; // Initialize health to maxHealth at the start
        rb = GetComponent<Rigidbody2D>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        Debug.Log(gameObject.name + " took " + damage + " damage. Remaining health: " + health);
        if (health <= 0)
        {
            Die();
        }
    }

    public IEnumerator TakeKnockback(Vector2 attackDir, float knockbackMult, float knockbackDur)
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

    private void Die()
    {
        Debug.Log(gameObject.name + " has died.");
        Destroy(gameObject);
    }
}
