using UnityEngine;
using UnityEngine.Events; // Required for UnityEvents

public class AnimationEvents : MonoBehaviour
{
    [Header("Animation Event Hooks")]
    // These will appear in the Inspector as assignable lists
    public UnityEvent onDeath;
    public UnityEvent onFootstep;
    public UnityEvent onAttackHit;

    // The Animation Window timeline calls these
    public void TriggerDeath()
    {
        onDeath?.Invoke(); 
    }

    public void TriggerFootstep()
    {
        onFootstep?.Invoke();
    }

    public void TriggerAttackHit()
    {
        onAttackHit?.Invoke();
    }


    //continoursly updated portion for jumpp
    private Animator _animator;
    private BaseEnemyAI _playerController;

    private readonly int _isJumpingHash = Animator.StringToHash("isJumping");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        
        // Reach up to the parent to find the main movement script
        _playerController = GetComponentInParent<BaseEnemyAI>();

        if (_playerController == null)
            Debug.LogError("PlayerAnimator cannot find PlayerController on the parent!");
    }

    private void Update()
    {
        // 1. "Pull" the variable from the main script
        bool jumpState = _playerController.isJumping;

        // 2. Feed it to the Animator
        _animator.SetBool(_isJumpingHash, jumpState);
    }
}