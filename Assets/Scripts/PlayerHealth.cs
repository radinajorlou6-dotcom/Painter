using UnityEngine;
using System.Collections;

//FOR NOW EXACT SAME AS ENEMY HEALTH

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float health;
    [SerializeField] private Animator anim;
    Rigidbody2D rb;
    private PlayerMovement playerMovement;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        health = maxHealth;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Spikes"))
        {
            TakeDamage(9999); //Instant death
        }
    }

    public void TakeDamage(float damage)
    {
        anim.SetTrigger("gotHurt");
        health -= damage;
        DebugUtils.Log(gameObject.name + " took " + damage + " damage. Remaining health: " + health);
        healthBar.UpdateHealthBar(health, maxHealth);
        if (health <= 0)
        {
            Die();
        }
    }

    public IEnumerator TakeKnockback(Vector2 attackDir, float knockbackMult, float knockbackDur)
    {
        if (rb == null) yield break;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        Vector2 initialVelocity = attackDir.normalized * knockbackMult;
        float elapsed = 0f;

        while (elapsed < knockbackDur)
        {
            if (this == null || rb == null) yield break;

            float t = elapsed / knockbackDur;
            rb.linearVelocity = Vector2.Lerp(initialVelocity, Vector2.zero, t * t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }
    }

    private void Die()
    {
        anim.SetTrigger("Died");
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        GetComponent<PlayerMovement>().enabled = false;
        this.enabled = false;
        Destroy(gameObject);
    }
}
