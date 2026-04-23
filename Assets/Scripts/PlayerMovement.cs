using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public Rigidbody2D rb;
    bool isFacingRight = true;

    //Movement variables
    [Header("Movement")]
    public float moveSpeed = 2f;

    //Jumping variables
    [Header("Jumping")]
    public float jump_height = 2f;
    float horizontalMovement;

    //Ground check variables
    [Header("GroundCheck")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(0.5f, 0.05f);
    public LayerMask groundLayer;
    bool isGrounded = true;


    //Wallcheck variables
    [Header("WallCheck")]
    public Transform wallCheck;
    public Vector2 wallCheckSize = new Vector2(0.5f, 0.05f);
    public LayerMask wallLayer;

    //Wall movement variables
    [Header("WallMovement")]
    public float wallSlideSpeed = 2f;
    bool isWallSliding;
    //Wall jump variables
    bool isWallJumping = false; //Prevents the player from changing direction mid air after a wall jump 
    float wallJumpDirection;
    float wallJumpTime = 0.35f;
    float wallJumpTimer;
    public Vector2 wallJumpPower = new Vector2(5f, 10f);

    //Gravity variables
    [Header("Gravity")]
    public float baseGravity = 2;
    public float maxFallSpeed = 18f;
    public float fallMultiplier = 2f;

    // Start is called; once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        GroundCheck(); //Check if player is on the ground
        Gravity(); //Apply custom gravity mechanics
        WallSlide(); //Apply wall sliding mechanics
        ProcessWallJump(); //Handle wall jump mechanics

        if (!isWallJumping) //Prevent horizontal movement control during wall jump
        { 
            rb.linearVelocity = new Vector2(horizontalMovement * moveSpeed, rb.linearVelocity.y);
            Flip(); //Flip player sprite based on movement direction
        }
            
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
        if (!isGrounded && WallCheck() && horizontalMovement != 0)
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

    private void CancelWallJump()
    {
        isWallJumping = false;
    }


    public void Move(InputAction.CallbackContext context)
    {
        horizontalMovement = context.ReadValue<Vector2>().x;
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed && isGrounded) //hold jump = full jump power
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jump_height);
        }
        else if (context.canceled && rb.linearVelocity.y >= 0) //if player taps rather than hold
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }   

        //Wall jump mechanics
        else if(context.performed && isWallSliding)
        {
            isWallJumping = true;
            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpPower.x, wallJumpPower.y); //Jump away from the wall
            wallJumpTimer = wallJumpTime; //Reset wall jump timer

            //Force Flip
            if (transform.localScale.x != wallJumpDirection)
            {
                FlipMain();
            }
        }

    
    }

    private void GroundCheck()
    {
        if(Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0, groundLayer))
        {
            isGrounded = true;
            isWallJumping = false; //Reset wall jump state when grounded
            wallJumpTimer = 0; //Reset wall jump timer when grounded
        }
        else
        {
            isGrounded = false;
        }
    }

    private bool WallCheck()
    {
        return Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0, wallLayer);
    }

    private void Flip()
    {
        if(isFacingRight && horizontalMovement < 0 || !isFacingRight && horizontalMovement > 0)
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
