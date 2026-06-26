using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    bool isFacingRight = true;

    //Movement variables
    [Header("Movement")]
    public float moveSpeed = 2f;
    [SerializeField] private Animator anim;

    //Jumping variables
    [Header("Jumping")]
    public float jump_height = 2f;
    float horizontalMovement;
    [Header("Physics")]
    [Tooltip("How quickly the player accelerates toward target ground speed (units/s^2)")]
    [SerializeField] private float groundAcceleration = 80f;
    [Tooltip("Ground friction: rate at which horizontal speed is reduced when there's no input (units/s^2)")]
    [SerializeField] private float groundFriction = 30f;
    [Tooltip("Air drag: rate at which horizontal speed decays while airborne (units/s^2)")]
    [SerializeField] private float airDrag = 5f;

    //Ground check variables
    [Header("GroundCheck")]
    [SerializeField] Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.05f);
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float maxSlopeAngle;
    private ContactFilter2D groundFilter;
    private ContactPoint2D[] groundContact;
    public bool isGrounded {get; private set;} = true;
    [SerializeField] private int maxPoints = 10;
    public string isGroundedOn {get; private set;}


    //Wallcheck variables
    [Header("WallCheck")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.5f, 0.05f);
    [SerializeField] private LayerMask wallLayer;
    public bool isWalled {get; private set;} = false;
    private Collider2D[] wallCheckArray;

    //Wall movement variables
    [Header("WallMovement")]
    public float wallSlideSpeed = 2f;
    bool isWallSliding;
    //Wall jump variables
    bool isWallJumping = false; //Prevents the player from changing direction mid air after a wall
    float wallJumpDirection;
    float wallJumpTime = 0.35f;
    float wallJumpTimer;
    [SerializeField] private Vector2 wallJumpPower = new Vector2(5f, 10f);

    //Gravity variables
    [Header("Gravity")]
    [SerializeField] private float baseGravity = 2;
    [SerializeField] private float maxFallSpeed = 18f;
    [SerializeField] private float fallMultiplier = 2f;

    //Slingshot variables
    [Header("Slingshot")]
    [Tooltip("If ON, the player can steer left/right during a slingshot launch. " +
             "If OFF, horizontal control is locked until landing so the launch momentum is fully preserved.")]
    [SerializeField] private bool allowDirectionChangeWhileSlingshotting = false;
    public bool isSlingshotting { get; private set;} = false;
    private bool slingshotHasLeftGround = false;

    void Awake()
    {
        groundFilter = new ContactFilter2D();
        groundContact = new ContactPoint2D[maxPoints];
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useLayerMask = true;
        groundFilter.SetNormalAngle(90f - maxSlopeAngle, 90f + maxSlopeAngle);
        groundFilter.useNormalAngle = true;

        wallCheckArray = new Collider2D[maxPoints];
    }

    // Update is called once per frame
    void Update()
    {
        ProcessWallJump();
        anim.SetFloat("Speed", Mathf.Abs(horizontalMovement));
        anim.SetBool("isGrounded", isGrounded);
        anim.SetFloat("yVelocity", rb.linearVelocity.y);

        if (!isWallJumping && !(isSlingshotting && !allowDirectionChangeWhileSlingshotting)) //Prevent flipping during wall jump or a locked slingshot launch
        { 
            Flip(); //Flip player sprite based on movement direction
        }
            
    }

    private void FixedUpdate()
    {
        GroundCheck();
        WallCheck();
        UpdateSlingshotState();

        if (!isWallJumping)
        {
            if (isWalled)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            else if (isSlingshotting)
            {
                ApplySlingshotMovement();
            }
            else
            {
                ApplyStandardMovement();
            }
        }

        Gravity();
        WallSlide();
    }

    // Normal grounded/air horizontal movement with acceleration and friction.
    private void ApplyStandardMovement()
    {
        if (Mathf.Abs(horizontalMovement) > 0.01f)
        {
            float desiredX = horizontalMovement * moveSpeed;
            float accel = groundAcceleration; //Same acceleration whether on ground or in air
            float newX = Mathf.MoveTowards(rb.linearVelocity.x, desiredX, accel * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }
        else
        {
            float drag = isGrounded ? groundFriction : airDrag;
            float newX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, drag * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }
    }

    // Horizontal movement while a slingshot launch is in flight.
    private void ApplySlingshotMovement()
    {
        // Locked mode: ignore horizontal input entirely so the launch momentum is preserved.
        if (!allowDirectionChangeWhileSlingshotting) return;

        // Steer mode: let input nudge the trajectory without braking it toward moveSpeed.
        // Pressing into the launch speeds it up; pressing against it bleeds speed off gradually.
        if (Mathf.Abs(horizontalMovement) > 0.01f)
        {
            float steeredX = rb.linearVelocity.x + horizontalMovement * airDrag * Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(steeredX, rb.linearVelocity.y);
        }
    }

    // Slingshot momentum stays active until the player has taken off and then landed again.
    private void UpdateSlingshotState()
    {
        if (!isSlingshotting) return;

        if (!isGrounded)
        {
            slingshotHasLeftGround = true;
            return;
        }

        // Back on the ground: end the slingshot once we've either landed from a real launch,
        // or the launch momentum has bled off to normal walking speed. The speed check covers
        // shallow launches that never actually left the ground, so control is handed back
        // instead of leaving the player frozen in the locked slingshot state.
        if (slingshotHasLeftGround || Mathf.Abs(rb.linearVelocity.x) <= moveSpeed)
        {
            isSlingshotting = false;
        }
    }

    // Called by SlingshotAbility the moment a launch impulse is applied.
    public void BeginSlingshot()
    {
        isSlingshotting = true;
        slingshotHasLeftGround = false;
    }

    //Different falling mechanics to make the game feel better. Increases fall speed the longer you fall, and caps it at a certain point.
    private void Gravity()
    {
      if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = baseGravity * fallMultiplier; //Fall increasingly faster
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -maxFallSpeed)); //Cap fall speed
        }
        else
        {
            rb.gravityScale = baseGravity; //Reset gravity when not falling
        }
    }

    private void WallSlide()
    {
        if (isWalled && horizontalMovement != 0)
        {
            isWallSliding = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed)); //Limit fall speed while wall sliding
        }
        else
        {
            isWallSliding = false;
        }
    }

    private void ProcessWallJump()
    {
        if (isWallSliding)
        {
            if (wallJumpTimer <= 0) isWallJumping = false;
            wallJumpDirection = -transform.localScale.x; //Jump in the opposite direction of the wall
        }
        if(wallJumpTimer > 0f)
        {
            wallJumpTimer -= Time.deltaTime; //Decrease wall jump timer    
        }
    }


    public void Move(InputAction.CallbackContext context)
    {
        if (context.canceled)
         {
            horizontalMovement = 0;
         }
         else
         {
            // Directly set horizontal input each frame to feed acceleration logic
            horizontalMovement = context.ReadValue<Vector2>().x;
         }
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if(context.performed && isWallSliding)
        {
            anim.SetTrigger("Jump");
            isWallJumping = true;
            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpPower.x, wallJumpPower.y); //Jump away from the wall
            wallJumpTimer = wallJumpTime; //Reset wall jump timer

            //Force Flip
            if (transform.localScale.x != wallJumpDirection)
            {
                FlipMain();
            }
        }
        else if (context.performed && isGrounded) //hold jump = full jump power
        {
            anim.SetTrigger("Jump");
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jump_height);
        }
        else if (context.canceled && rb.linearVelocity.y >= 0) //if player taps rather than hold
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }   

        //Wall jump mechanics
    }

    private void GroundCheck()
    {
        int count = rb.GetContacts(groundFilter, groundContact);
        if (count > 0)
        {
            isGrounded = true;
            isGroundedOn =  LayerMask.LayerToName(groundContact[0].collider.gameObject.layer);
            DebugUtils.Log($"Grounded on: {isGroundedOn}");
            isWallJumping = false;
            wallJumpTimer = 0;
            return;
        }
        isGrounded = false;
        isGroundedOn = string.Empty;
    }

    private void WallCheck()
    {
        if (Physics2D.OverlapBoxNonAlloc(wallCheck.position, wallCheckSize, 0f, wallCheckArray, wallLayer) > 0)
        {
            isWalled = true;
        }
        else
        {
            isWalled = false;
        }
    }

    private void Flip()
    {
        // Only allow flipping when grounded to prevent mid-air facing/instant direction changes

        if (isFacingRight && horizontalMovement < 0 || !isFacingRight && horizontalMovement > 0)
        {
            FlipMain();
        }
    }

    private void FlipMain()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(wallCheck.position, wallCheckSize);
    }


}
