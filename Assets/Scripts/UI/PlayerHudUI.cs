using UnityEngine;

/// <summary>
/// Drives the player's placeholder HUD bars: health, platform ink, shield and curse cooldown.
///
/// Polls each frame rather than subscribing to events. That's the right trade for a HUD: three of
/// these four resources have no change event to listen to (ink is a bare public float, shield hits
/// and the curse cooldown are plain fields), so subscribing would mean adding events across three
/// gameplay scripts purely to serve the UI. Reading four numbers a frame costs nothing and keeps
/// the dependency pointing one way — UI reads gameplay, gameplay never knows the UI exists.
///
/// Every bar is optional, so this can be dropped in with only the ones that are built so far.
/// </summary>
public class PlayerHudUI : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("The player. Leave empty to find it by the Player tag on the first frame.")]
    [SerializeField] private GameObject player;

    [Header("Bars")]
    [SerializeField] private ResourceBar healthBar;
    [Tooltip("Platform draw reserve — shows ink LEFT, so it drains as the player draws.")]
    [SerializeField] private ResourceBar platformInkBar;
    [Tooltip("Shield hits remaining before it breaks.")]
    [SerializeField] private ResourceBar shieldBar;
    [Tooltip("Curse readiness — fills back up as the cooldown expires.")]
    [SerializeField] private ResourceBar curseBar;

    [Header("Behaviour")]
    [Tooltip("Hide the ink, shield and curse bars until those abilities are unlocked, so the HUD " +
             "grows with the player instead of showing meters they can't use yet.\n\n" +
             "Off by default because it makes a fresh HUD look broken: with only one ability in " +
             "GameManager's Starting Abilities, three of the four bars vanish and it reads as the " +
             "UI not working. Turn it on once the bars are built and you want the real behaviour.")]
    [SerializeField] private bool hideLockedAbilities;

    private Health health;
    private CombatInput combatInput;
    private PlayerCombat playerCombat;
    private CurseAbility curse;

    private void Start()
    {
        ResolvePlayer();
    }

    /// <summary>
    /// Resolved in Start rather than Awake so the player is guaranteed to exist, and re-run from
    /// Update if it doesn't — the player is spawned in some scenes and the HUD shouldn't be the
    /// thing that breaks when it arrives a frame late.
    /// </summary>
    private void ResolvePlayer()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        health = player.GetComponentInChildren<Health>();
        combatInput = player.GetComponentInChildren<CombatInput>();
        playerCombat = player.GetComponentInChildren<PlayerCombat>();
        curse = player.GetComponentInChildren<CurseAbility>();

        // A bar with no source silently hides itself, which is indistinguishable from the HUD
        // being broken. Say once what was and wasn't found, so a missing bar has an answer.
        DebugUtils.Log(
            $"HUD bound to '{player.name}' — " +
            $"health: {(health != null ? "ok" : "MISSING")}, " +
            $"ink: {(combatInput != null ? "ok" : "MISSING")}, " +
            $"shield: {(playerCombat != null ? "ok" : "MISSING")}, " +
            $"curse: {(curse != null ? "ok" : "MISSING — add CurseAbility to the Player")}");
    }

    private void Update()
    {
        if (player == null)
        {
            ResolvePlayer();
            return;
        }

        UpdateHealth();
        UpdatePlatformInk();
        UpdateShield();
        UpdateCurse();
    }

    private void UpdateHealth()
    {
        if (healthBar == null) return;

        if (health == null) { healthBar.Show(false); return; }

        healthBar.SetValue(health.CurrentHealth, health.MaxHealth);
    }

    /// <summary>
    /// CombatInput counts ink *used*, so the bar is inverted — a reserve should drain as you spend
    /// it, not fill up. Showing the raw number would read backwards.
    /// </summary>
    private void UpdatePlatformInk()
    {
        if (platformInkBar == null) return;

        if (combatInput == null) { platformInkBar.Show(false); return; }

        if (hideLockedAbilities && !IsUnlocked(AbilityType.PlatformDraw))
        {
            platformInkBar.Show(false);
            return;
        }

        float remaining = combatInput.maxDrawInk - combatInput.currentDrawInk;
        platformInkBar.SetValue(remaining, combatInput.maxDrawInk);
    }

    private void UpdateShield()
    {
        if (shieldBar == null) return;

        if (playerCombat == null) { shieldBar.Show(false); return; }

        if (hideLockedAbilities && !IsUnlocked(AbilityType.ShieldDraw))
        {
            shieldBar.Show(false);
            return;
        }

        // Empty while replenishing: the shield genuinely isn't available then, and showing its
        // hit count would say otherwise.
        if (playerCombat.ShieldIsLocked)
        {
            shieldBar.SetFraction(0f);
            return;
        }

        shieldBar.SetValue(playerCombat.ShieldHitsRemaining, playerCombat.MaxShieldHits);
    }

    private void UpdateCurse()
    {
        if (curseBar == null) return;

        if (curse == null) { curseBar.Show(false); return; }

        if (hideLockedAbilities && !IsUnlocked(AbilityType.Curse))
        {
            curseBar.Show(false);
            return;
        }

        // Inverted so the bar fills as the curse becomes available — a cooldown that empties as it
        // recharges reads as the ability draining away.
        curseBar.SetFraction(1f - curse.CooldownRemaining01);
    }

    private static bool IsUnlocked(AbilityType ability)
    {
        return GameManager.Instance != null && GameManager.Instance.IsAbilityUnlocked(ability);
    }
}
