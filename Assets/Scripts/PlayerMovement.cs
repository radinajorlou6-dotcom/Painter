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
    public bool isGrounded {get; private set;} = true;


    //Wallcheck variables
    [Header("WallCheck")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.5f, 0.05f);
    [SerializeField] private LayerMask wallLayer;
    public bool isWalled {get; private set;} = false;
    public string isGroundedOn {get; private set;}

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


    // Update is called once per frame
    void Update()
    {
        ProcessWallJump();
        anim.SetFloat("Speed", Mathf.Abs(horizontalMovement));
        anim.SetBool("isGrounded", isGrounded);
        anim.SetFloat("yVelocity", rb.linearVelocity.y);

        if (!isWallJumping) //Prevent horizontal movement control during wall jump
        { 
            Flip(); //Flip player sprite based on movement direction
        }
            
    }

    private void FixedUpdate()
    {
        GroundCheck();
        WallCheck();

        if (!isWallJumping)
        {
            if (isWalled)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            else
            {
                if (Mathf.Abs(horizontalMovement) > 0.01f)
                {
                    float desiredX = horizontalMovement * moveSpeed;
                    float accel = isGrounded ? groundAcceleration : airDrag; 
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
        }

        Gravity();
        WallSlide();
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
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayer);
        filter.useLayerMask = true;

        ContactPoint2D[] contacts = new ContactPoint2D[10];
        int count = rb.GetContacts(filter, contacts);

        for (int i = 0; i < count; i++)
        {
            float slopeAngle = Vector2.Angle(contacts[i].normal, Vector2.up);
            if (slopeAngle <= maxSlopeAngle)
            {
                isGrounded = true;
                isGroundedOn =  LayerMask.LayerToName(contacts[i].collider.gameObject.layer);
                DebugUtils.Log($"Grounded on: {isGroundedOn}");
                isWallJumping = false;
                wallJumpTimer = 0;
                return;
            }
        }
        isGrounded = false;
        isGroundedOn = string.Empty;
    }

    private void WallCheck()
    {
        if (Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0, wallLayer))
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
