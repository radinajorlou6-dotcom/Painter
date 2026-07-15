using UnityEngine;
using System.Collections;

/// <summary>
/// Player-specific reactions to the shared <see cref="Health"/> component.
/// Health storage and the IHealth/IDamageable contract live on Health; this
/// script just listens for its events (update the bar, run the death sequence)
/// and provides the player's physics knockback.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerHealth : MonoBehaviour, IKnockbackable
{
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private Animator anim;

    [Header("Impact Feedback")]
    [Tooltip("Freeze-frame length when the player takes a non-fatal hit.")]
    [SerializeField] private float hurtHitStop = 0.06f;
    [Tooltip("Camera shake intensity/duration on death.")]
    [SerializeField] private float deathShake = 0.5f;
    [SerializeField] private float deathShakeDuration = 0.4f;

    private Health health;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private float lastHealth = -1f;

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void OnEnable()
    {
        health.HealthChanged += HandleHealthChanged;
        health.Died += HandleDeath;
    }

    private void OnDisable()
    {
        health.HealthChanged -= HandleHealthChanged;
        health.Died -= HandleDeath;
    }

    private void Start()
    {
        // Sync the bar to the starting value (Health raises its first event in
        // Awake, which may fire before we subscribed, so we prime it here).
        HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Spikes"))
        {
            health.Kill(); // instant death
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (healthBar != null) healthBar.UpdateHealthBar(current, max);

        // Freeze-frame when we actually lose health (ignore heals and the initial
        // Start sync). Death is handled separately with a bigger shake below.
        if (lastHealth >= 0f && current < lastHealth && current > 0f)
        {
            HitStop.Instance?.Freeze(hurtHitStop);
        }
        lastHealth = current;
    }

    private void HandleDeath()
    {
        CameraShake.Instance?.Shake(deathShake, deathShakeDuration);

        if (anim != null) anim.SetTrigger("Died");
        if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        if (playerMovement != null) playerMovement.enabled = false;
        GameManager.Instance?.UpdateGameState(GameManager.GameState.Died);
    }

    public IEnumerator TakeKnockback(Vector2 direction, float force, float duration)
    {
        if (rb == null) yield break;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        Vector2 initialVelocity = direction.normalized * force;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (this == null || rb == null) yield break;

            float t = elapsed / duration;
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

    private void DieDestroy() //Will be called by animator after final frame has played
    {
        this.enabled = false;
        Destroy(gameObject);
    }
}
