using UnityEngine;

/// <summary>
/// The boss's health bar. Hides itself whenever there is no boss to show — before the fight is
/// found, and once she's dead — so the same HUD prefab can sit in every level without a boss bar
/// hanging empty across the top of the screen.
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Leave empty to find the boss in the scene automatically.")]
    [SerializeField] private BossAI boss;

    [Header("Bars")]
    [SerializeField] private ResourceBar healthBar;

    [Tooltip("Optional second bar showing the weakpoints left during the Shielded phase. Hidden " +
             "outside that phase, when it would just be a full bar meaning nothing.")]
    [SerializeField] private ResourceBar weakpointBar;

    [Header("Visibility")]
    [Tooltip("Only show the bar once the player is actually fighting — i.e. the boss has taken " +
             "damage. Untick to show it from the moment the level loads.")]
    [SerializeField] private bool showOnlyAfterFirstHit = true;

    private Health bossHealth;
    private bool engaged;

    private void Start()
    {
        Resolve();
    }

    private void Resolve()
    {
        if (boss == null) boss = FindAnyObjectByType<BossAI>();
        if (boss != null && bossHealth == null) bossHealth = boss.GetComponent<Health>();
    }

    private void Update()
    {
        // The boss is destroyed on death, so this is also how the bar learns the fight is over.
        if (boss == null || bossHealth == null)
        {
            Hide();
            Resolve();
            return;
        }

        float fraction = bossHealth.MaxHealth > 0f
            ? bossHealth.CurrentHealth / bossHealth.MaxHealth
            : 0f;

        // "Has taken damage" stands in for "the fight has started". The boss stays dormant until
        // the player enters her arena, so a first hit is a reliable signal without the HUD having
        // to know anything about arenas.
        if (showOnlyAfterFirstHit && !engaged && fraction < 1f) engaged = true;

        if (showOnlyAfterFirstHit && !engaged)
        {
            Hide();
            return;
        }

        if (healthBar != null) healthBar.SetFraction(fraction);

        UpdateWeakpoints();
    }

    private void UpdateWeakpoints()
    {
        if (weakpointBar == null) return;

        if (boss.Phase != BossAI.BossPhase.Shielded)
        {
            weakpointBar.Show(false);
            return;
        }

        BossWeakpoint[] points = boss.GetComponentsInChildren<BossWeakpoint>(true);
        if (points.Length == 0)
        {
            weakpointBar.Show(false);
            return;
        }

        int alive = 0;
        foreach (BossWeakpoint point in points)
        {
            if (point != null && point.IsAlive) alive++;
        }

        weakpointBar.SetValue(alive, points.Length);
    }

    private void Hide()
    {
        if (healthBar != null) healthBar.Show(false);
        if (weakpointBar != null) weakpointBar.Show(false);
    }
}
