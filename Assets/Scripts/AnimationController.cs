using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every animation the character can play. Add new entries here and map them in
/// the inspector list on <see cref="AnimationController"/>; nothing else needs to change.
/// </summary>
public enum AnimationType
{

    //Shared
    Idle, GotHurt, Died,

    //Player 
    Run, Jump, Fall, Attack, Land,

    //KnightEnemy
    Walk, Charge, Bash,

    //TODO: BaseEnemy does not use this script yet might add later
}

/// <summary>
/// Code-driven animation hub. Gameplay scripts call <see cref="PlayAnimation"/> and this
/// class decides how to talk to the Animator — there is no state-graph / transition-arrow
/// wiring. Each <see cref="AnimationType"/> is mapped to an Animator state name in the
/// inspector and played directly with <see cref="Animator.Play(int,int,float)"/>, which
/// gives precise control over interruption instead of relying on transition conditions.
///
/// This class only knows about animation state. It never decides *when* to jump, attack,
/// etc. — that stays in the gameplay scripts that call it.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimationController : MonoBehaviour
{
    /// <summary>One row of the inspector map: which state name (and interrupt rule) an AnimationType uses.</summary>
    [System.Serializable]
    private struct AnimationEntry
    {
        public AnimationType type;

        [Tooltip("Exact name of the Animator state/clip to play for this type.")]
        public string stateName;

        [Tooltip("If true, once this animation starts nothing can interrupt it until it finishes " +
                 "(e.g. GotHurt, Died).")]
        public bool uninterruptible;
    }

    [Header("Animation Map")]
    [Tooltip("Map each AnimationType to an Animator state name. Tick 'uninterruptible' for ones " +
             "that must always play to completion.")]
    [SerializeField] private List<AnimationEntry> animations = new List<AnimationEntry>();

    [Tooltip("Which Animator layer these states live on (usually 0).")]
    [SerializeField] private int layer = 0;

    private Animator animator;

    // Resolved lookups built from the inspector list (string hashing done once, not per call).
    private readonly Dictionary<AnimationType, int> stateHashes = new Dictionary<AnimationType, int>();
    private readonly Dictionary<AnimationType, bool> uninterruptibleFlags = new Dictionary<AnimationType, bool>();

    private AnimationType currentType;
    private bool hasCurrent = false;

    // "Locked" == an uninterruptible animation is currently playing and owns the Animator.
    private bool isLocked = false;
    private Coroutine unlockRoutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        BuildLookup();
    }

    private void BuildLookup()
    {
        stateHashes.Clear();
        uninterruptibleFlags.Clear();

        foreach (AnimationEntry entry in animations)
        {
            if (string.IsNullOrEmpty(entry.stateName))
            {
                DebugUtils.LogWarning($"AnimationController: {entry.type} has no state name assigned.");
                continue;
            }
            stateHashes[entry.type] = Animator.StringToHash(entry.stateName);
            uninterruptibleFlags[entry.type] = entry.uninterruptible;
        }
    }

    /// <summary>
    /// Request an animation. If an uninterruptible animation is currently playing, the request
    /// is ignored — unless it's the same animation, which restarts cleanly (see class notes).
    /// Requesting the animation that's already playing is a no-op so looping states (Idle/Run)
    /// don't reset to frame zero every frame.
    /// </summary>
    public void PlayAnimation(AnimationType type)
    {
        if (isLocked)
        {
            // Only the same uninterruptible animation may restart; everything else waits its turn.
            if (hasCurrent && type == currentType)
            {
                StartAnimation(type);
            }
            return;
        }

        // Don't restart a state that's already playing (prevents locomotion from freezing on frame 0).
        if (hasCurrent && type == currentType) return;

        StartAnimation(type);
    }

    /// <summary>True while an uninterruptible animation (e.g. hurt/death) is playing.</summary>
    public bool IsPlayingUninterruptible() => isLocked;

    /// <summary>
    /// Optional precise-unlock hook: call this from an Animation Event placed on the last frame
    /// of an uninterruptible clip instead of relying on the measured clip length.
    /// </summary>
    public void NotifyAnimationFinished()
    {
        if (unlockRoutine != null)
        {
            StopCoroutine(unlockRoutine);
            unlockRoutine = null;
        }
        isLocked = false;
    }

    private void StartAnimation(AnimationType type)
    {
        if (!stateHashes.TryGetValue(type, out int hash))
        {
            DebugUtils.LogWarning($"AnimationController: no Animator state mapped for {type}.");
            return;
        }

        animator.Play(hash, layer, 0f); // always from the start
        currentType = type;
        hasCurrent = true;

        bool uninterruptible = uninterruptibleFlags.TryGetValue(type, out bool flag) && flag;
        if (uninterruptible)
        {
            LockUntilClipFinishes();
        }
        else
        {
            // A normal animation immediately clears any previous (already-finished) lock state.
            isLocked = false;
        }
    }

    private void LockUntilClipFinishes()
    {
        isLocked = true;
        if (unlockRoutine != null) StopCoroutine(unlockRoutine);
        unlockRoutine = StartCoroutine(UnlockAfterClip());
    }

    private IEnumerator UnlockAfterClip()
    {
        // Wait one frame so the Animator actually transitions into the new state and can
        // report the correct clip length.
        yield return null;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);

        // Clip length is reported at speed 1; divide by the effective speed to get real seconds.
        float speed = Mathf.Abs(info.speed * animator.speed);
        float duration = speed > 0.0001f ? info.length / speed : info.length;

        yield return new WaitForSeconds(duration);

        isLocked = false;
        unlockRoutine = null;
    }

    #region Weapon ability visuals (weapon rotates on a pivot — fill these in later)
    // These are intentionally empty. Gameplay scripts already call them at the right moments,
    // so once you implement the pivot rotation here the abilities animate with no further wiring.

    public void PlaySlash() { }

    public void PlayStab() { }

    public void PlayShieldDraw() { }

    public void StopShieldDraw() { }

    public void PlayPlatformDraw() { }

    public void StopPlatformDraw() { }
    #endregion
}
