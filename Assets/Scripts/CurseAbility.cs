using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hold the curse button, hover an enemy, release to freeze it where it stands — no movement, no
/// further attacks — for a set time, then a cooldown before it can be cast again.
///
/// Built on the same bones as <see cref="SlingshotAbility"/>: press to begin, aim with the mouse in
/// Update, release to commit, with a LineRenderer drawn from code so the gesture is visible. The
/// tether changes colour with whether the thing under the cursor can actually be cursed, because
/// "let go when the mouse is in the right place" only works if the right place is visible.
///
/// Missing costs nothing. The cooldown is stamped only on a stun that actually landed, so hunting
/// for a target with the button held isn't punished.
/// </summary>
public class CurseAbility : MonoBehaviour
{
    [Header("Curse")]
    [Tooltip("Seconds the enemy is held in place.")]
    [SerializeField] private float stunDuration = 3f;

    [Tooltip("Seconds before the curse can be cast again. Starts only on a successful stun.")]
    [SerializeField] private float curseCooldown = 5f;

    [Tooltip("What counts as a target. Set this to the Enemies layer — the enemy body colliders " +
             "live there, while the Knight's damage hitbox sits on Ignorables and must not match.")]
    [SerializeField] private LayerMask enemyLayers;

    [Header("Input")]
    [Tooltip("Name of the action in the Player map of PlayerControls. Bind it to whatever key you " +
             "like in the Input Actions asset — this only has to match the action's name.")]
    [SerializeField] private string curseActionName = "Curse";

    [Tooltip("Subscribe to that action directly. Untick only if you'd rather wire OnCurse into the " +
             "PlayerInput component's event list by hand — with both on, the curse would fire twice.")]
    [SerializeField] private bool autoBindAction = true;

    [Header("Tether")]
    [SerializeField] private Color validColour = new Color(0.75f, 0.3f, 1f, 1f);
    [SerializeField] private Color invalidColour = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    [SerializeField] private float tetherWidth = 0.08f;

    [Header("Feel")]
    [Tooltip("Frames of freeze on a successful curse. 0 for none.")]
    [SerializeField] private float castHitStop = 0.06f;
    [SerializeField] private float castShakeIntensity = 0.15f;
    [SerializeField] private float castShakeDuration = 0.15f;
    [Tooltip("Played on the cursed enemy's own AudioController. Pick from the types it already " +
             "has configured — a missing one warns once and is otherwise harmless.")]
    [SerializeField] private AudioType castSound = AudioType.GotHurt;

    private Camera mainCamera;
    private LineRenderer tether;
    private EnemyBase target;
    private bool isAiming;
    private float nextCurseTime;

    private InputAction curseAction;

    private void Awake()
    {
        mainCamera = Camera.main;
        CreateTether();
        HideTether();
    }

    private void OnEnable()
    {
        if (!autoBindAction) return;

        curseAction = ResolveAction();
        if (curseAction == null) return;

        curseAction.started += OnCurse;
        curseAction.canceled += OnCurse;
    }

    private void OnDisable()
    {
        if (curseAction != null)
        {
            curseAction.started -= OnCurse;
            curseAction.canceled -= OnCurse;
            curseAction = null;
        }

        // Losing the component mid-aim shouldn't strand the tether on screen.
        StopAiming();
    }

    /// <summary>
    /// Finds the action by name rather than relying on the PlayerInput component's serialized event
    /// list. That list is wired by dragging components into slots and silently breaks when one is
    /// moved — the UI/InstaKill entry on the Player prefab is already sitting there with no target.
    /// </summary>
    private InputAction ResolveAction()
    {
        PlayerInput input = GetComponentInParent<PlayerInput>();
        if (input == null || input.actions == null)
        {
            DebugUtils.LogWarning(
                $"CurseAbility on '{name}' found no PlayerInput, so the curse has no input. " +
                "Put it on the Player, or untick Auto Bind Action and wire OnCurse by hand.");
            return null;
        }

        InputAction action = input.actions.FindAction(curseActionName);
        if (action == null)
        {
            DebugUtils.LogWarning(
                $"CurseAbility can't find an action called '{curseActionName}'. Add it to the " +
                "Player map in PlayerControls — a new map wouldn't work, since GameManager only " +
                "enables the maps it knows by name.");
        }
        return action;
    }

    /// <summary>
    /// Also public so it can be dropped into a PlayerInput event slot instead, for anyone who
    /// prefers the inspector wiring the rest of the project uses. Untick Auto Bind Action first.
    /// </summary>
    public void OnCurse(InputAction.CallbackContext context)
    {
        if (context.started) BeginAiming();
        else if (context.canceled) ReleaseCurse();
    }

    private void BeginAiming()
    {
        // Gate only the press. SlingshotAbility gates the whole handler, so an ability lost
        // mid-gesture swallows the release too and leaves it stuck aiming forever.
        if (GameManager.Instance == null) return;
        if (!GameManager.Instance.IsAbilityUnlocked(AbilityType.Curse)) return;
        if (Time.time < nextCurseTime) return;

        isAiming = true;
        if (tether != null) tether.enabled = true;
    }

    private void Update()
    {
        if (!isAiming) return;

        target = FindTargetUnderCursor(out Vector2 cursorWorld);
        DrawTether(cursorWorld, target != null);
    }

    private void ReleaseCurse()
    {
        if (!isAiming) return;

        EnemyBase cursed = target;
        StopAiming();

        // A miss is free: no stun, no cooldown, nothing to undo.
        if (cursed == null) return;

        cursed.ApplyStun(stunDuration);
        nextCurseTime = Time.time + curseCooldown;

        if (castHitStop > 0f) HitStop.Instance?.Freeze(castHitStop);
        if (castShakeIntensity > 0f) CameraShake.Instance?.Shake(castShakeIntensity, castShakeDuration);
        cursed.GetComponent<AudioController>()?.Play(castSound);

        DebugUtils.Log($"Cursed {cursed.name} for {stunDuration}s");
    }

    private void StopAiming()
    {
        isAiming = false;
        target = null;
        HideTether();
    }

    /// <summary>
    /// Whatever enemy the mouse is over, or null. Two things have to hold: something on the enemy
    /// layer is under the cursor, and it is actually on screen — the mouse can sit outside the
    /// viewport, and cursing something the player can't see isn't the ability they asked for.
    /// </summary>
    private EnemyBase FindTargetUnderCursor(out Vector2 cursorWorld)
    {
        cursorWorld = Vector2.zero;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null || Mouse.current == null) return null;

        cursorWorld = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        Collider2D[] hits = Physics2D.OverlapPointAll(cursorWorld, enemyLayers);
        foreach (Collider2D hit in hits)
        {
            // Search upward: the collider that was hit is often a child of the enemy root.
            EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
            if (enemy == null) continue;
            if (!IsOnScreen(enemy.transform.position)) continue;

            return enemy;
        }

        return null;
    }

    private bool IsOnScreen(Vector3 worldPosition)
    {
        Vector3 viewport = mainCamera.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f
               && viewport.x >= 0f && viewport.x <= 1f
               && viewport.y >= 0f && viewport.y <= 1f;
    }

    private void CreateTether()
    {
        GameObject holder = new GameObject("CurseTether");
        holder.transform.SetParent(transform);

        tether = holder.AddComponent<LineRenderer>();
        tether.positionCount = 2;
        tether.useWorldSpace = true;
        tether.loop = false;
        tether.startWidth = tetherWidth;
        tether.endWidth = tetherWidth;
        tether.numCapVertices = 4;
        tether.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void DrawTether(Vector2 cursorWorld, bool valid)
    {
        if (tether == null) return;

        // Ends on the enemy rather than the cursor when there's a target, so the tether snaps to
        // what will actually be cursed instead of wherever the mouse happens to be inside it.
        Vector2 end = valid ? (Vector2)target.transform.position : cursorWorld;
        Color colour = valid ? validColour : invalidColour;

        tether.startColor = colour;
        tether.endColor = colour;
        tether.SetPosition(0, transform.position);
        tether.SetPosition(1, end);
    }

    private void HideTether()
    {
        if (tether != null) tether.enabled = false;
    }
}
