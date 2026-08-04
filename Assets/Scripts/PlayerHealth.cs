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
    [SerializeField] private AnimationController animController;

    [Header("Impact Feedback")]
    [Tooltip("Freeze-frame length when the player takes a non-fatal hit.")]
    [SerializeField] private float hurtHitStop = 0.06f;
    [Tooltip("Camera shake intensity/duration on death.")]
    [SerializeField] private float deathShake = 0.5f;
    [SerializeField] private float deathShakeDuration = 0.4f;

    [Header("Death Sequence")]
    [Tooltip("Seconds to let the death animation play before the screen fades out.")]
    [SerializeField] private float deathAnimationDuration = 1f;
    [Tooltip("Seconds for the screen to fade to/from black.")]
    [SerializeField] private float fadeDuration = 0.75f;
    [Tooltip("The death menu panel (Respawn / Exit buttons). Shown after the fade.")]
    [SerializeField] private GameObject deathMenu;

    private Health health;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private float lastHealth = -1f;
    private Vector3 initialSpawn;

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        if (animController == null) animController = GetComponent<AnimationController>();

        // Fallback respawn point if the player dies before reaching any checkpoint.
        initialSpawn = transform.position;
        if (deathMenu != null) deathMenu.SetActive(false);
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

    // Spike death lives on the Health component now, so it applies to every entity with health
    // instead of only the ones that remembered to write this handler.

    private void HandleHealthChanged(float current, float max)
    {
        if (healthBar != null) healthBar.UpdateHealthBar(current, max);

        // Freeze-frame when we actually lose health (ignore heals and the initial
        // Start sync). Death is handled separately with a bigger shake below.
        if (lastHealth >= 0f && current < lastHealth && current > 0f)
        {
            HitStop.Instance?.Freeze(hurtHitStop);
            animController?.PlayAnimation(AnimationType.GotHurt);
        }
        lastHealth = current;
    }

    private void HandleDeath()
    {
        CameraShake.Instance?.Shake(deathShake, deathShakeDuration);
        animController?.PlayAnimation(AnimationType.Died);

        // Stop the player from acting, but DON'T freeze the game yet — the death animation
        // and the fade both need real time to play out first.
        if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        if (playerMovement != null) playerMovement.enabled = false;

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // 1) Let the death animation play. Real time, since nothing is frozen yet.
        yield return new WaitForSecondsRealtime(deathAnimationDuration);

        // 2) Fade the screen to black.
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut(fadeDuration);

        // 3) Now freeze the game, hand input to the UI, and present the choices.
        GameManager.Instance?.UpdateGameState(GameManager.GameState.Died);
        if (deathMenu != null) deathMenu.SetActive(true);
    }

    /// <summary>Hooked to the "Respawn" button. Returns the player to the last checkpoint.</summary>
    public void Respawn()
    {
        StartCoroutine(RespawnSequence());
    }

    private IEnumerator RespawnSequence()
    {
        if (deathMenu != null) deathMenu.SetActive(false);

        // Move to the last checkpoint (or the level's start), and reset physics + health.
        Vector3 target = (GameManager.Instance != null && GameManager.Instance.HasCheckpoint)
            ? GameManager.Instance.LastCheckpoint
            : initialSpawn;
        transform.position = target;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        health.ResetHealth();
        lastHealth = health.CurrentHealth;

        // Clear the death pose and drop back to idle.
        animController?.NotifyAnimationFinished();
        animController?.PlayAnimation(AnimationType.Idle);

        // Unfreeze the game and give control back.
        GameManager.Instance?.UpdateGameState(GameManager.GameState.Playing);
        if (playerMovement != null) playerMovement.enabled = true;

        // Fade back in.
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeIn(fadeDuration);
    }

    /// <summary>Hooked to the "Exit" button.</summary>
    public void ExitGame()
    {
        GameManager.Instance?.QuitGame();
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

    // The player is no longer destroyed on death — it respawns instead. Kept as a harmless
    // no-op so any leftover "DieDestroy" animation event on the death clip doesn't throw.
    private void DieDestroy() { }
}
