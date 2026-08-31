using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A three-phase duelling boss that teleports between fixed anchors instead of walking.
///
/// The fight reads as a loop with two interruptions:
///
///   Duel      idle tell -> attack -> cooldown -> blink to a random anchor -> repeat
///   Shielded  at 66% health. Stops blinking, invulnerable except at its weakpoints.
///             Destroying every weakpoint returns it to the duel.
///   Frenzy    at 33% health. Cycles the anchors in a fixed, learnable order and is
///             invulnerable. Three landed stuns end it; each one opens a window of real
///             vulnerability. Afterwards the duel resumes at max aggression.
///
/// Both interruptions are one-way doors — the thresholds latch, so healing can't replay a phase.
///
/// The boss's own collider is never disabled, including mid-teleport. Fading is done on the
/// renderers alone. That is deliberate: the curse targets by hovering a collider, so a boss that
/// switched its collider off while blinking would be untargetable at exactly the moments the
/// player is most likely to be aiming at it.
/// </summary>
public class BossAI : EnemyBase
{
    public enum BossPhase { Duel, Shielded, Frenzy }

    // ---------------------------------------------------------------- Teleporting

    [Header("Arena")]
    [Tooltip("The boss only acts while the player is inside this volume. Outside it she stands " +
             "idle: no attacks, no teleporting, no frenzy blinking.\n\n" +
             "Point it at any Collider2D — the box on the room's CameraRoom works well, since the " +
             "fight and the camera framing then share exactly the same footprint. Leave it empty " +
             "and she is active everywhere, which is the old behaviour.")]
    [SerializeField] private Collider2D arena;

    [Tooltip("Barriers that seal the arena once the player is inside — a door, a wall, a slab of " +
             "collider across each entrance. Switched OFF at startup so the player can walk in, " +
             "ON the moment they do, and OFF again when the boss dies.\n\n" +
             "Put them just outside the Arena collider's edges. A barrier that overlaps where the " +
             "player is standing when it appears will shove them.")]
    [SerializeField] private GameObject[] arenaBarriers;

    [Tooltip("Untick to leave the arena open and let the player walk out mid-fight.")]
    [SerializeField] private bool lockArenaOnEntry = true;

    [Tooltip("Start the whole fight over when the player dies: barriers down, health and phases " +
             "restored, weakpoints back, boss returned to her first anchor.\n\n" +
             "This is what stops a sealed arena becoming a dead end. The player respawns at their " +
             "last checkpoint, which is almost always outside the arena, so without a reset the " +
             "barriers would still be up with no way back in.")]
    [SerializeField] private bool resetOnPlayerDeath = true;

    [Header("Teleport Anchors")]
    [Tooltip("The fixed spots the boss occupies. Frenzy walks this list in order, so the order " +
             "here is the pattern the player learns.")]
    [SerializeField] private Transform[] anchors = new Transform[4];

    [Tooltip("Seconds to fade out and back in around a blink. The collider stays live throughout.")]
    [SerializeField] private float teleportFadeDuration = 0.15f;

    // ---------------------------------------------------------------- Duel phase

    [Header("Duel — Timing")]
    [Tooltip("The tell. The boss stands still this long before committing to an attack, and this " +
             "pause is the player's whole window to read and react.")]
    [SerializeField] private float idlePauseDuration = 0.5f;

    [Tooltip("Seconds after an attack lands before the boss blinks away.")]
    [SerializeField] private float attackCooldown = 0.6f;

    [Tooltip("Every duel timing is divided by this once the Frenzy phase has been beaten.")]
    [SerializeField] private float maxAggressionSpeedMultiplier = 1.5f;

    [Header("Duel — Melee")]
    [Tooltip("Inside this distance the boss commits to melee instead of a projectile.")]
    [SerializeField] private float meleeRange = 3f;
    [SerializeField] private float meleeWindup = 0.35f;
    [SerializeField] private float meleeRadius = 1.5f;
    [SerializeField] private float meleeDamage = 20f;
    [SerializeField] private float meleeKnockback = 12f;
    [SerializeField] private float meleeKnockbackDuration = 0.2f;
    [Tooltip("Where the melee overlap is centred. Falls back to the boss's own position.")]
    [SerializeField] private Transform meleePoint;

    [Header("Duel — Ranged")]
    [SerializeField] private float rangedWindup = 0.45f;
    [SerializeField] private float projectileSpeed = 14f;
    [Tooltip("Pool of projectiles. Their Bullet component must have Fired By set to Enemy, or the " +
             "shots will pass through the player and hurt the boss's own side.")]
    [SerializeField] private ObjectPooling projectilePool;
    [SerializeField] private Transform firePoint;

    [Header("Duel — Spikes")]
    [Tooltip("Spike prefab with a BossSpike on it. Leave empty to disable the attack entirely.")]
    [SerializeField] private GameObject spikePrefab;

    [Tooltip("Height of the arena floor the spikes come out of. Leave empty to use the bottom of " +
             "the Arena collider, which is right when the arena box sits on the ground.")]
    [SerializeField] private Transform spikeFloorLevel;

    [Tooltip("World units between spikes. The wave spans the whole width of the Arena collider, " +
             "so this is the only thing deciding how dense it is.")]
    [SerializeField] private float spikeSpacing = 1.5f;

    [Tooltip("Seconds between one spike erupting and the next along the row. 0 erupts the whole " +
             "floor at once, which can only be jumped; raise it to make a wave that sweeps across " +
             "the arena and can be outrun.")]
    [SerializeField] private float spikeSweepDelay;

    [Tooltip("Seconds the spikes stay up and dangerous once fully out. Leave at -1 to use whatever " +
             "each spike prefab is set to; anything else overrides it for the whole wave.")]
    [SerializeField] private float spikeActiveDuration = 1.2f;

    [Tooltip("Hard cap on spikes per wave, so a wide arena with tight spacing can't spawn hundreds " +
             "of objects in a frame.")]
    [SerializeField] private int maxSpikesPerWave = 60;

    [Tooltip("Minimum seconds between spike attacks. This is the 'every once in a while' — when " +
             "it has elapsed the next attack is spikes, whatever the distance to the player.")]
    [SerializeField] private float spikeAttackInterval = 8f;

    [Tooltip("Seconds she spends in the summoning animation before the spikes appear. The spikes " +
             "then telegraph again on their own as they rise.")]
    [SerializeField] private float spikeWindup = 0.5f;

    // ---------------------------------------------------------------- Shielded phase

    [Header("Shielded Phase")]
    [Tooltip("Fraction of max health that opens the Shielded phase.")]
    [Range(0f, 1f)][SerializeField] private float shieldedThreshold = 0.66f;

    [Tooltip("Leave empty to collect every BossWeakpoint under this object at startup.")]
    [SerializeField] private List<BossWeakpoint> weakpoints = new List<BossWeakpoint>();

    // ---------------------------------------------------------------- Frenzy phase

    [Header("Frenzy Phase")]
    [Range(0f, 1f)][SerializeField] private float frenzyThreshold = 0.33f;

    [Tooltip("Seconds the boss spends at each anchor while frenzied.")]
    [SerializeField] private float frenzyBlinkInterval = 0.8f;

    [Tooltip("Stuns needed to break the frenzy.")]
    [SerializeField] private int frenzyStunsRequired = 3;

    [Tooltip("Seconds of real vulnerability each landed stun buys. Independent of how long the " +
             "stun itself holds the boss.")]
    [SerializeField] private float stunVulnerabilityWindow = 3f;

    // ---------------------------------------------------------------- Animation placeholders

    [Header("Animation")]
    [Tooltip("Leave empty to use the AnimationController on this object.")]
    [SerializeField] private AnimationController animController;
    [SerializeField] private AnimationType idleAnimation = AnimationType.Idle;
    [SerializeField] private AnimationType meleeAnimation = AnimationType.BossMelee;
    [SerializeField] private AnimationType rangedAnimation = AnimationType.BossRanged;
    [SerializeField] private AnimationType teleportOutAnimation = AnimationType.BossTeleportOut;
    [SerializeField] private AnimationType teleportInAnimation = AnimationType.BossTeleportIn;
    [SerializeField] private AnimationType shieldedAnimation = AnimationType.BossShielded;
    [SerializeField] private AnimationType frenzyAnimation = AnimationType.BossFrenzy;
    [SerializeField] private AnimationType spikeAnimation = AnimationType.BossSpikes;

    [Header("Facing")]
    [Tooltip("Turn to face the player every frame. The base enemy only turns on its detection " +
             "tick and only while it can see the player — fine for something that walks, wrong for " +
             "a boss who teleports behind you and would keep facing the wrong way until the next tick.")]
    [SerializeField] private bool alwaysFacePlayer = true;

    [Tooltip("How far off centre the player has to be before she turns. Stops her flipping back " +
             "and forth every frame when the player is standing directly on top of her.")]
    [SerializeField] private float facingDeadzone = 0.3f;

    [Tooltip("Played whenever she loses health. Skipped while stunned, so a hit landed on a frozen " +
             "boss doesn't snap her out of the stun pose. Untick Play Hurt Animation if the flinch " +
             "reads badly during long attacks.")]
    [SerializeField] private bool playHurtAnimation = true;
    [SerializeField] private AnimationType hurtAnimation = AnimationType.GotHurt;

    [Tooltip("Played once on death, before the body is cleared away. EnemyBase also fires the " +
             "Animator's \"Died\" trigger if one is wired, so either route works.")]
    [SerializeField] private AnimationType deathAnimation = AnimationType.Died;

    // The stun pose is inherited: see Stun Animation on EnemyBase, shared with every other enemy.

    // ---------------------------------------------------------------- Events

    [Header("Death Drop")]
    [Tooltip("Dropped when the boss dies — put a KeyPickup on it. Spawned unparented so it " +
             "outlives the boss, which destroys itself once its death animation has played.")]
    [SerializeField] private GameObject keyPrefab;

    [Tooltip("Where the key lands. Falls back to the boss's own position. Aim it at solid ground " +
             "— the key is placed directly, so a drop point over a pit loses it.")]
    [SerializeField] private Transform keyDropPoint;

    [Header("Phase Events")]
    [Tooltip("Hooks for audio and VFX. Fired after the phase's own setup, so anything they read " +
             "about the boss is already in its new state.")]
    public UnityEvent OnDuelPhaseStarted;
    public UnityEvent OnShieldedPhaseStarted;
    public UnityEvent OnFrenzyPhaseStarted;
    public UnityEvent OnWeakpointDestroyed;
    public UnityEvent OnFrenzyStunLanded;
    public UnityEvent OnKeyDropped;
    [Tooltip("Fires as the spikes actually erupt, after the summoning windup — cue the rumble here.")]
    public UnityEvent OnSpikeAttack;

    [Tooltip("Fires when the player first enters the arena and the fight actually begins, and " +
             "again on each re-entry. Cue the music here rather than on scene load.")]
    public UnityEvent OnBossActivated;

    // ---------------------------------------------------------------- State

    public BossPhase Phase { get; private set; } = BossPhase.Duel;
    public bool IsMaxAggression { get; private set; }

    private bool shieldedPhaseUsed;
    private bool frenzyPhaseUsed;
    private int frenzyStunsLanded;
    private int currentAnchor = -1;
    private Renderer[] renderers;

    /// <summary>Last seen health, so HealthChanged can tell damage apart from a heal.</summary>
    private float lastHealth = -1f;

    /// <summary>Earliest Time.time the spike attack may come round again.</summary>
    private float nextSpikeTime;

    /// <summary>Latched once the barriers go up, so they can't flicker on a frame at the edge.</summary>
    private bool arenaLocked;

    /// <summary>The player's health, watched so the fight can reset when they die.</summary>
    private Health playerHealth;

    protected override void Awake()
    {
        base.Awake();

        if (animController == null) animController = GetComponent<AnimationController>();
        renderers = GetComponentsInChildren<Renderer>(true);

        // Open at startup whatever state they were left in in the editor, so the player can always
        // get in — a barrier accidentally saved as active would wall the fight off entirely.
        SetBarriers(false);

        if (weakpoints.Count == 0) weakpoints.AddRange(GetComponentsInChildren<BossWeakpoint>(true));
        foreach (BossWeakpoint weakpoint in weakpoints)
        {
            if (weakpoint == null) continue;
            weakpoint.Destroyed += HandleWeakpointDestroyed;
            weakpoint.SetExposed(false);
        }

        health.HealthChanged += HandleHealthChanged;

        if (anchors == null || anchors.Length == 0)
        {
            DebugUtils.LogWarning($"Boss '{name}' has no teleport anchors — it will never move.");
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (health != null) health.HealthChanged -= HandleHealthChanged;
        if (playerHealth != null) playerHealth.Died -= HandlePlayerDied;
        foreach (BossWeakpoint weakpoint in weakpoints)
        {
            if (weakpoint != null) weakpoint.Destroyed -= HandleWeakpointDestroyed;
        }
    }

    private void Start()
    {
        ResolvePlayerHealth();
        EnterDuelPhase();
    }

    /// <summary>
    /// Subscribes to the player's death. Resolved in Start rather than Awake so the player is
    /// certain to exist, and falls back to the tag when the boss's player reference wasn't set.
    /// </summary>
    private void ResolvePlayerHealth()
    {
        if (!resetOnPlayerDeath) return;

        GameObject playerObject = player != null
            ? player.gameObject
            : GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            DebugUtils.LogWarning($"Boss '{name}' found no player, so the fight can't reset on death.");
            return;
        }

        playerHealth = playerObject.GetComponentInChildren<Health>();
        if (playerHealth != null) playerHealth.Died += HandlePlayerDied;
    }

    private void HandlePlayerDied()
    {
        if (health.IsDead) return; // she won; nothing to reset

        DebugUtils.Log($"Player died — resetting the fight with '{name}'.");
        ResetFight();
    }

    /// <summary>
    /// Puts the fight back to its opening state. Everything that latches has to be cleared here,
    /// or a second attempt starts part-way through: the phase flags, the frenzy stun count, max
    /// aggression, the arena lock, and any weakpoints already broken.
    /// </summary>
    private void ResetFight()
    {
        SetBarriers(false);
        arenaLocked = false;

        shieldedPhaseUsed = false;
        frenzyPhaseUsed = false;
        frenzyStunsLanded = 0;
        IsMaxAggression = false;

        foreach (BossWeakpoint weakpoint in weakpoints)
        {
            if (weakpoint != null) weakpoint.Revive();
        }

        health.Invulnerable = false;
        health.ResetHealth();
        lastHealth = health.CurrentHealth;

        // Back to where she started, so the player doesn't return to find her standing over the
        // arena entrance.
        if (anchors != null && anchors.Length > 0 && anchors[0] != null)
        {
            transform.position = anchors[0].position;
            currentAnchor = 0;
        }

        SetRendererAlpha(1f);

        // Last, because it culls every coroutine and restarts the duel from a clean state.
        EnterDuelPhase();
    }

    /// <summary>
    /// The base Update is still wanted for its stun-animation upkeep, but its ground and wall
    /// probes are not — a teleporting boss has no groundCheck or collideCheck transform assigned,
    /// and the base would dereference them. Both are overridden to nothing below.
    /// </summary>
    protected override void GroundCheck() { }
    protected override void CollideCheck() { }

    /// <summary>
    /// The arena lock is driven from Update rather than from the phase coroutines, because it has
    /// to react to the player crossing the line and nothing else — a boss who happens to be
    /// stunned or mid-phase-change as they walk in must still shut the door behind them.
    /// </summary>
    protected override void Update()
    {
        base.Update();

        FacePlayer();

        if (!lockArenaOnEntry || arenaLocked) return;
        if (!IsPlayerInArena()) return;

        SetBarriers(true);
        arenaLocked = true;
        DebugUtils.Log($"Arena sealed behind the player — {arenaBarriers.Length} barrier(s) up.");
    }

    /// <summary>
    /// Turns to face the player. Runs every frame rather than on the detection tick, and ignores
    /// line of sight — she is a boss in a sealed room, so "can I see them" is never the question,
    /// and a teleport that lands behind the player has to be corrected the same frame it happens.
    ///
    /// Goes through the base Flip so dirIsRight stays in step; everything that reads facing —
    /// the charge direction, the melee point, the fire point — keys off that.
    /// </summary>
    private void FacePlayer()
    {
        if (!alwaysFacePlayer || player == null) return;

        float delta = player.position.x - transform.position.x;

        // Inside the deadzone she keeps whichever way she was already facing, rather than
        // flip-flopping every frame while the player stands on top of her.
        if (Mathf.Abs(delta) < facingDeadzone) return;

        bool playerIsRight = delta > 0f;
        if (playerIsRight != dirIsRight) Flip();
    }

    private void SetBarriers(bool active)
    {
        if (arenaBarriers == null) return;

        foreach (GameObject barrier in arenaBarriers)
        {
            if (barrier != null) barrier.SetActive(active);
        }
    }

    // ================================================================ Phase transitions

    /// <summary>
    /// Health crossing a threshold is what drives the fight forward. Each phase latches, so
    /// healing back above a threshold can't replay a phase that has already been beaten, and the
    /// lower threshold is tested first so a single huge hit skips straight to Frenzy rather than
    /// queueing both.
    /// </summary>
    private void HandleHealthChanged(float current, float max)
    {
        if (max <= 0f) return;

        // Flinch only on real damage: heals and the priming event Health raises on Awake both
        // arrive here too, and a stunned boss must keep her stun pose rather than shrugging it off.
        if (playHurtAnimation && current < lastHealth && current > 0f && !IsStunned)
        {
            animController?.PlayAnimation(hurtAnimation);
        }
        lastHealth = current;

        float fraction = current / max;

        if (!frenzyPhaseUsed && fraction <= frenzyThreshold)
        {
            frenzyPhaseUsed = true;
            // Crossing straight past the Shielded window means it is spent, not owed.
            shieldedPhaseUsed = true;
            EnterFrenzyPhase();
            return;
        }

        if (!shieldedPhaseUsed && fraction <= shieldedThreshold)
        {
            shieldedPhaseUsed = true;
            EnterShieldedPhase();
        }
    }

    /// <summary>
    /// Every phase change goes through here, because a phase must never inherit work from the last
    /// one — a windup already in flight would otherwise resolve its damage during a phase that is
    /// supposed to be locked.
    ///
    /// StopAllCoroutines is blunt enough to take two things with it that must come back:
    ///   - DetectionRoutine, started once in EnemyBase.Awake and never restarted, so without this
    ///     the boss would be permanently blind — canSeePlayer and the distance it picks attacks
    ///     with would both freeze at whatever they were.
    ///   - The stun routine, which restores the Rigidbody constraints on its final line. Killed
    ///     mid-stun it never gets there and the boss stays frozen forever, so the stun is released
    ///     *before* the cull rather than after.
    /// </summary>
    private void BeginPhase(BossPhase phase)
    {
        EndStun();
        StopAllCoroutines();
        StartCoroutine(DetectionRoutine());

        Phase = phase;
        CancelActions();
    }

    private void EnterDuelPhase()
    {
        BeginPhase(BossPhase.Duel);

        health.Invulnerable = false;
        foreach (BossWeakpoint weakpoint in weakpoints)
        {
            if (weakpoint != null) weakpoint.SetExposed(false);
        }

        DebugUtils.Log($"Boss entering Duel phase (max aggression: {IsMaxAggression})");
        OnDuelPhaseStarted?.Invoke();

        StartCoroutine(DuelRoutine());
    }

    private void EnterShieldedPhase()
    {
        BeginPhase(BossPhase.Shielded);

        // Invulnerable everywhere except the weakpoints, which carry their own Health and so are
        // damaged through the ordinary IDamageable path rather than anything special here.
        health.Invulnerable = true;
        foreach (BossWeakpoint weakpoint in weakpoints)
        {
            if (weakpoint != null) weakpoint.SetExposed(true);
        }

        DebugUtils.Log($"Boss entering Shielded phase with {CountLivingWeakpoints()} weakpoints");
        OnShieldedPhaseStarted?.Invoke();

        StartCoroutine(ShieldedRoutine());
    }

    private void EnterFrenzyPhase()
    {
        BeginPhase(BossPhase.Frenzy);

        health.Invulnerable = true;
        frenzyStunsLanded = 0;

        DebugUtils.Log($"Boss entering Frenzy phase — {frenzyStunsRequired} stuns to break it");
        OnFrenzyPhaseStarted?.Invoke();

        StartCoroutine(FrenzyRoutine());
    }

    // ================================================================ Duel

    /// <summary>
    /// Idle tell -> attack -> cooldown -> blink, forever. Every wait is scaled by aggression so the
    /// whole loop tightens after the Frenzy phase without any of the individual numbers changing.
    /// </summary>
    private IEnumerator DuelRoutine()
    {
        while (Phase == BossPhase.Duel)
        {
            // Held by a curse: the loop stalls entirely rather than pushing on. The Rigidbody
            // constraints a stun applies stop physics moving the boss, but a teleport writes
            // transform.position directly and would walk straight through them.
            yield return WaitUntilActive();

            // The tell. Deliberately a plain wait with the idle animation showing: it is the only
            // part of the loop the player can read, so nothing else should happen during it.
            animController?.PlayAnimation(idleAnimation);
            yield return WaitScaled(idlePauseDuration);

            if (CanUseSpikes())
            {
                // Spikes take priority when they come round, and ignore distance entirely — that
                // is what makes them the answer to a player who has settled into one safe range.
                yield return StartCoroutine(SpikeAttackRoutine());
            }
            else
            {
                // Otherwise distance decides, not the AI's mood — the player can position to force
                // whichever one they would rather deal with.
                float distance = player != null
                    ? Vector2.Distance(transform.position, player.position)
                    : float.PositiveInfinity;

                yield return distance <= meleeRange
                    ? StartCoroutine(BaseAttack())
                    : StartCoroutine(RangedAttackRoutine());
            }

            yield return WaitScaled(attackCooldown);

            yield return WaitUntilActive();
            yield return StartCoroutine(TeleportRoutine(PickRandomAnchor()));
        }
    }

    /// <summary>
    /// Blocks until the boss is allowed to act — the stun has run its course and the player is in
    /// the arena. Sits in front of everything that moves or commits, which is everywhere the
    /// Rigidbody constraints alone wouldn't hold her.
    /// </summary>
    private IEnumerator WaitUntilActive()
    {
        bool wasActive = true;

        while (IsStunned || !IsPlayerInArena())
        {
            wasActive = false;
            yield return null;
        }

        // Fires on the transition back in, so the first entry to the arena can cue music or a roar
        // without anything having to poll for it.
        if (!wasActive) OnBossActivated?.Invoke();
    }

    /// <summary>
    /// No arena assigned means active everywhere, so an existing boss keeps behaving as it did.
    /// A missing player counts as outside — better a dormant boss than one swinging at nothing.
    /// </summary>
    private bool IsPlayerInArena()
    {
        if (arena == null) return true;
        if (player == null) return false;

        return arena.OverlapPoint(player.position);
    }

    /// <summary>The melee. Wind up, then check what is in reach on the active frame, not before.</summary>
    protected override IEnumerator BaseAttack()
    {
        animController?.PlayAnimation(meleeAnimation);
        yield return WaitScaled(meleeWindup);

        // The phase can change during the windup, and a hit that lands after that would be a hit
        // the player had every reason to think was cancelled. A curse landing mid-windup counts
        // the same way — that is what makes it an interrupt rather than a brief pause.
        if (Phase != BossPhase.Duel || IsStunned || health.IsDead || !IsPlayerInArena()) yield break;

        Vector2 centre = meleePoint != null ? (Vector2)meleePoint.position : (Vector2)transform.position;
        Collider2D hit = Physics2D.OverlapCircle(centre, meleeRadius, playerLayer);
        if (hit == null) yield break;

        hit.GetComponent<IDamageable>()?.TakeDamage(meleeDamage);

        if (hit.GetComponent<IKnockbackable>() is IKnockbackable knockable)
        {
            Vector2 direction = ((Vector2)hit.transform.position - centre).normalized;
            StartCoroutine(knockable.TakeKnockback(direction, meleeKnockback, meleeKnockbackDuration));
        }
    }

    /// <summary>
    /// Ready when the interval has elapsed and there is something to erupt. Checked rather than
    /// rolled at random so the attack is on a rhythm the player can feel coming.
    /// </summary>
    private bool CanUseSpikes()
    {
        return spikePrefab != null && Time.time >= nextSpikeTime;
    }

    /// <summary>
    /// Summons a handful of spikes out of the floor. The boss plays a summoning animation, then
    /// each spike telegraphs again on its own as it rises — two beats of warning, because the
    /// attack covers ground the player can't simply back away from.
    /// </summary>
    private IEnumerator SpikeAttackRoutine()
    {
        // Stamped up front, not on completion: an attack interrupted by a phase change or a curse
        // shouldn't leave spikes queued to fire the instant the boss recovers.
        nextSpikeTime = Time.time + spikeAttackInterval;

        animController?.PlayAnimation(spikeAnimation);
        yield return WaitScaled(spikeWindup);

        if (Phase != BossPhase.Duel || IsStunned || health.IsDead || !IsPlayerInArena()) yield break;

        OnSpikeAttack?.Invoke();
        yield return StartCoroutine(SpawnSpikeWave());
    }

    /// <summary>
    /// Fills the whole arena floor with spikes, spaced evenly across the Arena collider's width.
    ///
    /// With no sweep delay the entire floor erupts at once, and the only answer is to be in the
    /// air — that is the attack, and it is why the windup has to be generous. A sweep delay turns
    /// it into a wave rolling across the arena that can be outrun instead.
    /// </summary>
    private IEnumerator SpawnSpikeWave()
    {
        // Without an arena there is no width to fill, so fall back to a patch around the boss
        // rather than doing nothing and leaving the attack silently broken.
        Bounds bounds = arena != null
            ? arena.bounds
            : new Bounds(transform.position, new Vector3(20f, 6f, 0f));

        float floorY = spikeFloorLevel != null ? spikeFloorLevel.position.y : bounds.min.y;
        float spacing = Mathf.Max(0.25f, spikeSpacing);

        int count = Mathf.Clamp(Mathf.FloorToInt(bounds.size.x / spacing), 1, Mathf.Max(1, maxSpikesPerWave));

        // Inset by half a step so the row sits inside the arena rather than straddling its walls.
        float startX = bounds.min.x + spacing * 0.5f;

        DebugUtils.Log($"Boss '{name}' erupting {count} spikes across the arena floor");

        for (int i = 0; i < count; i++)
        {
            Vector3 where = new Vector3(startX + i * spacing, floorY, 0f);

            // Unparented, so a spike outlives the boss's death rather than vanishing mid-eruption.
            GameObject spike = Instantiate(spikePrefab, where, Quaternion.identity);

            // Between Instantiate and the spike's own Start, which is the window where its timing
            // can still be changed before the eruption begins.
            if (spike.TryGetComponent(out BossSpike spikeScript))
            {
                spikeScript.SetActiveDuration(spikeActiveDuration);
            }

            // A stun or a phase change part-way through cuts the wave short, so a curse landed as
            // it starts is worth something rather than only stopping the next one.
            if (spikeSweepDelay <= 0f) continue;
            if (IsStunned || Phase != BossPhase.Duel) yield break;

            yield return new WaitForSeconds(spikeSweepDelay);
        }
    }

    private IEnumerator RangedAttackRoutine()
    {
        animController?.PlayAnimation(rangedAnimation);
        yield return WaitScaled(rangedWindup);

        if (Phase != BossPhase.Duel || IsStunned || health.IsDead || player == null) yield break;

        // Walking out of the arena mid-windup cancels the shot rather than letting it chase.
        if (!IsPlayerInArena()) yield break;
        if (projectilePool == null)
        {
            DebugUtils.LogWarning($"Boss '{name}' has no projectile pool, so its ranged attack does nothing.");
            yield break;
        }

        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        GameObject shot = projectilePool.SpawnFromPool(origin, Quaternion.identity);
        if (shot == null) yield break; // pool refuses to spawn while the game isn't playing

        // After the spawn, never before: Bullet zeroes its own velocity in OnEnable, which
        // SpawnFromPool triggers, so a velocity set earlier would be wiped.
        if (shot.TryGetComponent(out Rigidbody2D shotBody))
        {
            shotBody.linearVelocity = ((Vector2)player.position - origin).normalized * projectileSpeed;
        }
    }

    // ================================================================ Shielded

    /// <summary>
    /// Holds position and does nothing but wait to be dismantled. No teleporting and no attacking
    /// is the point — the phase is a pause in the duel that the player controls the length of.
    /// </summary>
    private IEnumerator ShieldedRoutine()
    {
        animController?.PlayAnimation(shieldedAnimation);

        // A boss with no weakpoints configured would otherwise sit here invulnerable forever.
        if (CountLivingWeakpoints() == 0)
        {
            DebugUtils.LogWarning($"Boss '{name}' entered the Shielded phase with no weakpoints. " +
                                  "Returning to the duel so the fight isn't stuck.");
            EnterDuelPhase();
            yield break;
        }

        while (CountLivingWeakpoints() > 0) yield return null;

        DebugUtils.Log("Boss shield broken — back to the duel.");
        EnterDuelPhase();
    }

    private void HandleWeakpointDestroyed(BossWeakpoint weakpoint)
    {
        OnWeakpointDestroyed?.Invoke();
    }

    private int CountLivingWeakpoints()
    {
        int alive = 0;
        foreach (BossWeakpoint weakpoint in weakpoints)
        {
            if (weakpoint != null && weakpoint.IsAlive) alive++;
        }
        return alive;
    }

    // ================================================================ Frenzy

    /// <summary>
    /// Blinks the anchors in list order — fixed and repeating, so the pattern is learnable and the
    /// player can set up ahead of the boss rather than react to it.
    ///
    /// The wait between blinks is an explicit frame loop rather than WaitForSeconds because the
    /// stun check has to run every frame. A stun landed between two blinks would otherwise be over
    /// before anything looked at it, and the player would see their curse do nothing.
    /// </summary>
    private IEnumerator FrenzyRoutine()
    {
        animController?.PlayAnimation(frenzyAnimation);

        bool wasStunned = false;

        while (Phase == BossPhase.Frenzy)
        {
            // A stun has to actually stop the blinking, or the reward for landing one is nothing
            // but a colour change on the health bar.
            yield return WaitUntilActive();
            yield return StartCoroutine(TeleportRoutine(NextAnchorInOrder()));

            float elapsed = 0f;
            while (elapsed < frenzyBlinkInterval && Phase == BossPhase.Frenzy)
            {
                // Rising edge only: IsStunned stays true for the whole stun, and counting it every
                // frame would clear the phase off a single curse.
                if (IsStunned && !wasStunned)
                {
                    wasStunned = true;
                    frenzyStunsLanded++;

                    DebugUtils.Log($"Frenzy stun {frenzyStunsLanded}/{frenzyStunsRequired}");
                    OnFrenzyStunLanded?.Invoke();

                    // A separate window from the stun itself, so how long the boss is *hittable*
                    // can be tuned without changing how long the curse holds it.
                    StartCoroutine(VulnerabilityWindow());

                    if (frenzyStunsLanded >= frenzyStunsRequired)
                    {
                        IsMaxAggression = true;
                        EnterDuelPhase();
                        yield break;
                    }
                }
                else if (!IsStunned)
                {
                    wasStunned = false;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    /// <summary>Drops invulnerability for a moment after a stun lands, then puts it back.</summary>
    private IEnumerator VulnerabilityWindow()
    {
        health.Invulnerable = false;
        yield return new WaitForSeconds(stunVulnerabilityWindow);

        // Only re-arm if the phase still wants it — the third stun ends Frenzy, and re-arming
        // afterwards would leave the boss invulnerable through the whole final duel.
        if (Phase == BossPhase.Frenzy) health.Invulnerable = true;
    }

    // ================================================================ Teleporting

    /// <summary>
    /// Fades out, moves, fades back in. Only the renderers are touched — the collider stays on
    /// throughout so the boss can still be hover-targeted by the curse mid-blink.
    /// </summary>
    private IEnumerator TeleportRoutine(int anchorIndex)
    {
        if (anchors == null || anchorIndex < 0 || anchorIndex >= anchors.Length) yield break;
        Transform destination = anchors[anchorIndex];
        if (destination == null) yield break;

        animController?.PlayAnimation(teleportOutAnimation);
        yield return StartCoroutine(FadeRenderers(1f, 0f));

        transform.position = destination.position;
        currentAnchor = anchorIndex;

        // Teleporting with velocity left over would have the boss drift away from the anchor it
        // just arrived at.
        if (rb != null) rb.linearVelocity = Vector2.zero;

        animController?.PlayAnimation(teleportInAnimation);
        yield return StartCoroutine(FadeRenderers(0f, 1f));
    }

    private IEnumerator FadeRenderers(float from, float to)
    {
        if (teleportFadeDuration <= 0f)
        {
            SetRendererAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < teleportFadeDuration)
        {
            elapsed += Time.deltaTime;
            SetRendererAlpha(Mathf.Lerp(from, to, elapsed / teleportFadeDuration));
            yield return null;
        }

        SetRendererAlpha(to);
    }

    private void SetRendererAlpha(float alpha)
    {
        foreach (Renderer r in renderers)
        {
            if (r is SpriteRenderer sprite && sprite != null)
            {
                Color colour = sprite.color;
                colour.a = alpha;
                sprite.color = colour;
            }
        }
    }

    /// <summary>Random, but never the anchor already occupied — a blink that goes nowhere reads as a bug.</summary>
    private int PickRandomAnchor()
    {
        if (anchors == null || anchors.Length == 0) return -1;
        if (anchors.Length == 1) return 0;

        int index;
        do
        {
            index = Random.Range(0, anchors.Length);
        }
        while (index == currentAnchor);

        return index;
    }

    /// <summary>Frenzy's fixed rotation through the anchor list, in Inspector order.</summary>
    private int NextAnchorInOrder()
    {
        if (anchors == null || anchors.Length == 0) return -1;
        return (currentAnchor + 1) % anchors.Length;
    }

    // ================================================================ Death

    /// <summary>
    /// Drops the key before handing over to the base, which stops every coroutine, strips the
    /// boss's physical presence and schedules it for destruction. Dropping first means the key
    /// exists no matter what happens during that teardown.
    /// </summary>
    protected override void HandleDeath()
    {
        // Before the base, which stops every coroutine and starts the destroy timer. The Animator
        // keeps playing through that, so the clip has the whole deathAnimationDuration to run.
        animController?.PlayAnimation(deathAnimation);

        // The fight is over, so let them out. Doing this on death rather than on picking up the
        // key means a player who dies to something else afterwards isn't sealed in with nothing
        // left to fight.
        SetBarriers(false);
        arenaLocked = false;

        DropKey();
        base.HandleDeath();
    }

    private void DropKey()
    {
        if (keyPrefab == null)
        {
            DebugUtils.LogWarning($"Boss '{name}' died with no Key Prefab assigned — nothing to " +
                                  "pick up, so the level can't be finished.");
            return;
        }

        Vector3 where = keyDropPoint != null ? keyDropPoint.position : transform.position;

        // Deliberately no parent: parenting it to the boss would destroy the key along with the
        // corpse a second later.
        Instantiate(keyPrefab, where, Quaternion.identity);

        DebugUtils.Log($"Boss '{name}' dropped its key at {where}");
        OnKeyDropped?.Invoke();
    }

    // ================================================================ Shared

    /// <summary>
    /// Every duel wait goes through here so max aggression is one multiplier rather than a
    /// second set of numbers to keep in step with the first.
    /// </summary>
    private WaitForSeconds WaitScaled(float seconds)
    {
        float scale = IsMaxAggression ? Mathf.Max(0.01f, maxAggressionSpeedMultiplier) : 1f;
        return new WaitForSeconds(seconds / scale);
    }

    /// <summary>
    /// Called by EnemyBase when a stun lands, and on every phase change. There is no attack
    /// coroutine handle to cancel here because BeginPhase and the stun both arrive via
    /// StopAllCoroutines — what is left is making sure nothing stays half-applied.
    /// </summary>
    protected override void CancelActions()
    {
        SetRendererAlpha(1f);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (anchors != null)
        {
            Gizmos.color = Color.magenta;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] == null) continue;
                Gizmos.DrawWireSphere(anchors[i].position, 0.4f);

                // Draw the frenzy order as a loop, since that order is a design decision.
                Transform next = anchors[(i + 1) % anchors.Length];
                if (next != null) Gizmos.DrawLine(anchors[i].position, next.position);
            }
        }

        Gizmos.color = Color.red;
        Vector3 centre = meleePoint != null ? meleePoint.position : transform.position;
        Gizmos.DrawWireSphere(centre, meleeRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
}
