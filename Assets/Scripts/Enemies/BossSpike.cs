using System.Collections;
using UnityEngine;

/// <summary>
/// One spike erupting from the floor as part of the boss's spike attack. Spawned by
/// <see cref="BossAI"/>, lives its whole life on its own, and destroys itself afterwards.
///
/// It rises before it can hurt anything. That gap is the attack — a spike that damaged on the frame
/// it spawned would be unreactable, and the whole point of telegraphing from the floor is that the
/// player gets to move. Damage is off during the rise and switches on only once it's fully out.
///
/// Nothing here reads the boss, so a spike outlives her death harmlessly and the prefab can be
/// dropped into a level by hand for testing.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossSpike : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds spent emerging. Harmless throughout — this is the window the player reacts in.")]
    [SerializeField] private float riseDuration = 0.45f;

    [Tooltip("Seconds fully out and dangerous.")]
    [SerializeField] private float activeDuration = 1.2f;

    [Tooltip("Seconds spent sinking back down. Harmless again as soon as it starts.")]
    [SerializeField] private float retractDuration = 0.3f;

    [Header("Motion")]
    [Tooltip("How far below its spawn point the spike starts. Should be at least the height of " +
             "the art, or the tip pokes out of the floor before the attack begins.")]
    [SerializeField] private float riseHeight = 1.5f;

    [Header("Damage")]
    [SerializeField] private float damage = 15f;
    [Tooltip("Who it can hurt. Set to the Player layer.")]
    [SerializeField] private LayerMask damageLayers;
    [Tooltip("Damage the player once per eruption rather than every frame they touch it.")]
    [SerializeField] private bool damageOncePerEruption = true;

    private Collider2D hitbox;
    private Vector3 topPosition;
    private bool isDangerous;
    private bool hasDamaged;

    private void Awake()
    {
        hitbox = GetComponent<Collider2D>();
        hitbox.isTrigger = true;

        // Where it was spawned is where it ends up; it starts that far below and climbs.
        topPosition = transform.position;
        transform.position = topPosition + Vector3.down * riseHeight;
    }

    /// <summary>
    /// Overrides how long this spike stays dangerous, so one field on the boss can govern a whole
    /// wave instead of the value being buried in the prefab.
    ///
    /// Safe to call straight after Instantiate: Awake has already run by then, and Start — which
    /// begins the eruption — has not.
    /// </summary>
    public void SetActiveDuration(float seconds)
    {
        if (seconds >= 0f) activeDuration = seconds;
    }

    private void Start()
    {
        StartCoroutine(Erupt());
    }

    private IEnumerator Erupt()
    {
        yield return Move(topPosition + Vector3.down * riseHeight, topPosition, riseDuration);

        isDangerous = true;

        // Catches a player already standing in it when it finishes rising. OnTriggerEnter2D won't
        // fire for someone who never moved, so without this the safest place to stand would be
        // directly on top of a spike.
        DamageOverlapping();

        yield return new WaitForSeconds(activeDuration);

        isDangerous = false;
        yield return Move(topPosition, topPosition + Vector3.down * riseHeight, retractDuration);

        Destroy(gameObject);
    }

    private IEnumerator Move(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        transform.position = to;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Stay as well as Enter: the player can walk into a spike that is already up, and can also
        // be knocked into one without a fresh Enter firing.
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        if (!isDangerous) return;
        if (damageOncePerEruption && hasDamaged) return;
        if ((damageLayers.value & (1 << other.gameObject.layer)) == 0) return;

        IDamageable target = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        if (target == null) return;

        target.TakeDamage(damage);
        hasDamaged = true;
    }

    private void DamageOverlapping()
    {
        ContactFilter2D filter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
        filter.SetLayerMask(damageLayers);

        Collider2D[] results = new Collider2D[4];
        int count = hitbox.Overlap(filter, results);

        for (int i = 0; i < count; i++)
        {
            TryDamage(results[i]);
        }
    }
}
