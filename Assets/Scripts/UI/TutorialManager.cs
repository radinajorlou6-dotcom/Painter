using UnityEngine;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Gameplay moments a tutorial prompt can react to. Most are raised by gameplay code through
/// <see cref="TutorialManager.Report"/>; Moved is measured by the manager itself so the most
/// common "walk a bit to dismiss this" case needs no edits to PlayerMovement.
/// </summary>
public enum TutorialSignal
{
    Moved,          // accumulated horizontal distance in world units
    Jumped,
    Landed,
    Attacked,
    PlatformDrawn,
    ShieldRaised,
    Slingshotted,
    Interacted,
}

/// <summary>How a prompt decides it should appear or disappear.</summary>
public enum TutorialConditionType
{
    Never,              // hide: stays up forever. show: disabled.
    Immediately,        // as soon as the prompt is eligible (show) / the frame it appears (hide)
    Signal,             // a TutorialSignal reaches Amount
    AbilityUnlocked,
    ColourUnlocked,
    Seconds,
    Zone,               // player is inside a TutorialZone with the matching id
    PromptFinished,     // another prompt in this list has been dismissed
}

/// <summary>
/// One show-or-hide test. Only the field matching <see cref="type"/> is read, so the unused
/// ones in the Inspector are harmless noise — Unity has no way to hide them without a custom
/// drawer, and a drawer is more machinery than this earns.
/// </summary>
[System.Serializable]
public class TutorialCondition
{
    public TutorialConditionType type = TutorialConditionType.Immediately;

    [Tooltip("Signal type. Used when Type is Signal.")]
    public TutorialSignal signal;

    [Tooltip("How much of the signal is needed — world units for Moved, a count for everything " +
             "else. Also doubles as the delay in seconds when Type is Seconds.")]
    public float amount = 1f;

    [Tooltip("Used when Type is AbilityUnlocked.")]
    public AbilityType ability;

    [Tooltip("Used when Type is ColourUnlocked.")]
    public PaintColour colour;

    [Tooltip("Id of a TutorialZone in the scene. Used when Type is Zone.")]
    public string zoneId;

    [Tooltip("Id of another prompt in this list. Used when Type is PromptFinished.")]
    public string promptId;
}

/// <summary>A single piece of tutorial text plus the rules for when it comes and goes.</summary>
[System.Serializable]
public class TutorialPrompt
{
    [Tooltip("Identifier other prompts can point at with a PromptFinished condition. Optional " +
             "unless something references it.")]
    public string id;

    [TextArea(2, 4)]
    [Tooltip("What the player reads. e.g. \"Use A and D to move left and right\"")]
    public string message;

    [Header("Rules")]
    public TutorialCondition showWhen = new TutorialCondition();
    public TutorialCondition hideWhen = new TutorialCondition();

    [Header("Timing")]
    [Tooltip("Seconds to wait after the show condition passes before the text actually appears.")]
    public float showDelay = 0f;

    [Tooltip("Shortest time the prompt stays up once shown, even if the hide condition is already " +
             "true. Stops a prompt flashing for one frame when the player was already mid-action.")]
    public float minVisibleSeconds = 1f;

    [Tooltip("Leave off and the prompt is shown once per scene load. Turn on for something the " +
             "player should be reminded of every time.")]
    public bool repeatable = false;
}

/// <summary>
/// Drives contextual tutorial text. Author prompts in the Inspector: each one gets a show
/// condition and a hide condition, so "tell them how to move, then shut up once they've moved"
/// needs no code.
///
/// One prompt is visible at a time. When several become eligible at once the earliest in the
/// list wins and the rest wait their turn, so prompts can't stack on top of each other.
///
/// Scene-scoped rather than persistent: tutorial state is per-level, and letting the counters
/// reset on load is what makes a prompt repeatable across a retry.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    private enum PromptState
    {
        Idle,       // show condition not met yet
        Pending,    // show condition met, waiting out showDelay
        Visible,
        Done,
    }

    public static TutorialManager Instance { get; private set; }

    [Header("UI")]
    [Tooltip("Panel holding the tutorial text. Faded in and out rather than toggled so the " +
             "prompt doesn't pop.")]
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private TMP_Text label;

    [Tooltip("Seconds for the fade in either direction.")]
    [SerializeField] private float fadeDuration = 0.25f;

    [Header("Prompts")]
    [Tooltip("Evaluated top to bottom. When two are eligible at once the higher one shows first.")]
    [SerializeField] private List<TutorialPrompt> prompts = new List<TutorialPrompt>();

    [Header("Player Tracking")]
    [Tooltip("Left empty, the player is found by the Player tag on Start.")]
    [SerializeField] private Transform player;

    private readonly Dictionary<TutorialSignal, float> signals = new Dictionary<TutorialSignal, float>();
    private readonly HashSet<string> occupiedZones = new HashSet<string>();
    private PromptState[] states;
    private float[] stateEnteredAt;
    private float[] hideBaselines;

    private int visibleIndex = -1;
    private float lastPlayerX;
    private float targetAlpha;

    private void Awake()
    {
        // Only remove the duplicate component, never the GameObject it lives on — it may be
        // carrying the tutorial Canvas with it.
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        states = new PromptState[prompts.Count];
        stateEnteredAt = new float[prompts.Count];
        hideBaselines = new float[prompts.Count];

        for (int i = 0; i < prompts.Count; i++)
        {
            stateEnteredAt[i] = Time.unscaledTime;
        }
    }

    private void Start()
    {
        if (player == null)
        {
            // Found by component rather than by tag: GroundCheck and WallCheck are tagged Player
            // too, and FindGameObjectWithTag would happily hand back one of those children.
            PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
            if (movement != null) player = movement.transform;
        }

        if (player != null) lastPlayerX = player.position.x;

        // Start hidden without a fade, so a prompt whose show condition is Immediately still
        // animates in rather than being visible on frame one.
        if (panel != null) panel.alpha = 0f;
        targetAlpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        TrackMovement();

        // Don't advance tutorial logic while paused or dead — but keep fading, so a prompt
        // that was mid-transition when the player paused finishes cleanly.
        bool playing = GameManager.Instance == null ||
                       GameManager.Instance.CurrentState == GameManager.GameState.Playing;

        if (playing) EvaluatePrompts();

        UpdateFade();
    }

    // --- Signals ---

    /// <summary>
    /// Tells the tutorial system something happened. Static and null-safe so gameplay code can
    /// call it unconditionally, the same way HitStop.Instance?.Freeze is called.
    /// Pass an amount for continuous signals; the default of 1 suits discrete events.
    /// </summary>
    public static void Report(TutorialSignal signal, float amount = 1f)
    {
        if (Instance == null) return;

        Instance.signals.TryGetValue(signal, out float current);
        Instance.signals[signal] = current + amount;
    }

    private float GetSignal(TutorialSignal signal)
    {
        signals.TryGetValue(signal, out float value);
        return value;
    }

    /// <summary>Called by <see cref="TutorialZone"/> as the player crosses its trigger.</summary>
    public void SetZoneOccupied(string zoneId, bool occupied)
    {
        if (string.IsNullOrEmpty(zoneId)) return;

        if (occupied) occupiedZones.Add(zoneId);
        else occupiedZones.Remove(zoneId);
    }

    // Measured here rather than reported from PlayerMovement, because "walk a little" is the
    // single most common dismissal rule and this keeps it zero-touch. Horizontal only: falling
    // down a shaft shouldn't count as learning to walk.
    private void TrackMovement()
    {
        if (player == null) return;

        float x = player.position.x;
        Report(TutorialSignal.Moved, Mathf.Abs(x - lastPlayerX));
        lastPlayerX = x;
    }

    // --- Evaluation ---

    private void EvaluatePrompts()
    {
        for (int i = 0; i < prompts.Count; i++)
        {
            switch (states[i])
            {
                case PromptState.Idle:
                    if (IsMet(prompts[i].showWhen, i, forShowing: true)) EnterState(i, PromptState.Pending);
                    break;

                case PromptState.Pending:
                    // Another prompt is on screen — wait rather than fighting it for the slot.
                    if (visibleIndex != -1) break;
                    if (Time.unscaledTime - stateEnteredAt[i] < prompts[i].showDelay) break;
                    Show(i);
                    break;

                case PromptState.Visible:
                    if (Time.unscaledTime - stateEnteredAt[i] < prompts[i].minVisibleSeconds) break;
                    if (IsMet(prompts[i].hideWhen, i, forShowing: false)) Hide(i);
                    break;
            }
        }
    }

    private bool IsMet(TutorialCondition condition, int index, bool forShowing)
    {
        switch (condition.type)
        {
            case TutorialConditionType.Never:
                return false;

            case TutorialConditionType.Immediately:
                return true;

            case TutorialConditionType.Signal:
                // While showing, count only what the player did *after* the prompt appeared.
                // Without the baseline, "move 2 units to dismiss" would be satisfied instantly
                // by any prompt that appears after the player has already walked around.
                float progress = GetSignal(condition.signal) - (forShowing ? 0f : hideBaselines[index]);
                return progress >= condition.amount;

            case TutorialConditionType.AbilityUnlocked:
                // Polled rather than event-driven: GameManager.UnlockAbility raises nothing,
                // and polling avoids having to widen its API for this.
                return GameManager.Instance != null &&
                       GameManager.Instance.IsAbilityUnlocked(condition.ability);

            case TutorialConditionType.ColourUnlocked:
                return GameManager.Instance != null &&
                       GameManager.Instance.IsColourUnlocked(condition.colour);

            case TutorialConditionType.Seconds:
                return Time.unscaledTime - stateEnteredAt[index] >= condition.amount;

            case TutorialConditionType.Zone:
                return occupiedZones.Contains(condition.zoneId);

            case TutorialConditionType.PromptFinished:
                return IsPromptDone(condition.promptId);
        }

        return false;
    }

    private bool IsPromptDone(string promptId)
    {
        if (string.IsNullOrEmpty(promptId)) return false;

        for (int i = 0; i < prompts.Count; i++)
        {
            if (prompts[i].id == promptId) return states[i] == PromptState.Done;
        }

        return false;
    }

    // --- Display ---

    private void Show(int index)
    {
        visibleIndex = index;
        EnterState(index, PromptState.Visible);

        // Freeze the "how much has happened so far" mark for the hide test.
        hideBaselines[index] = GetSignal(prompts[index].hideWhen.signal);

        if (label != null) label.text = prompts[index].message;
        targetAlpha = 1f;

        DebugUtils.Log($"Tutorial: showing \"{prompts[index].message}\"");
    }

    private void Hide(int index)
    {
        EnterState(index, prompts[index].repeatable ? PromptState.Idle : PromptState.Done);

        if (visibleIndex == index)
        {
            visibleIndex = -1;
            targetAlpha = 0f;
        }
    }

    private void EnterState(int index, PromptState state)
    {
        states[index] = state;
        stateEnteredAt[index] = Time.unscaledTime;
    }

    // Unscaled so a prompt still fades during a HitStop freeze or a pause.
    private void UpdateFade()
    {
        if (panel == null) return;

        float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
        panel.alpha = Mathf.MoveTowards(panel.alpha, targetAlpha, step);
    }

    /// <summary>Hides whatever is on screen and lets it re-trigger. For a checkpoint respawn.</summary>
    public void ResetPrompts()
    {
        for (int i = 0; i < prompts.Count; i++)
        {
            EnterState(i, PromptState.Idle);
        }

        signals.Clear();
        visibleIndex = -1;
        targetAlpha = 0f;
    }
}
