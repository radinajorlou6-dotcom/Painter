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
    private Rigidbody2D _playerRb;

    private readonly int _isGroundedHash = Animator.StringToHash("isGrounded");
    private readonly int _yVelocity = Animator.StringToHash("yVelocity");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        
        // Reach up to the parent to find the main movement script
        _playerController = GetComponentInParent<BaseEnemyAI>();
        _playerRb = GetComponentInParent<Rigidbody2D>();

        if (_playerController == null)
            Debug.LogError("PlayerAnimator cannot find PlayerController on the parent!");
    }

    private void Update()
    {
        // 1. "Pull" the variable from the main script
        bool groundState = _playerController.isGrounded;
        float yVelocity = _playerRb.linearVelocity.y;

        // 2. Feed it to the Animator
        _animator.SetBool(_isGroundedHash, groundState);
        _animator.SetFloat(_yVelocity, yVelocity);
    }
}