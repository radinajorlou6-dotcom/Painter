using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

//[RequireComponent(typeof(AudioController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    bool isFacingRight = true;

    //Movement variables
    [Header("Movement")]
    public float moveSpeed = 2f;
    [SerializeField] private AnimationController animController;
    [SerializeField] private AudioController audioController;
    private bool isWalking;

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

    // Unfiltered by normal: groundFilter discards steep contacts by design, so seeing them at all
    // needs a second pass.
    private ContactFilter2D surfaceFilter;
    private ContactPoint2D[] surfaceContact;
    private bool onSteepSlope;
    private Vector2 steepSlopeNormal;
    [Tooltip("Downhill acceleration applied on a slope too steep to stand on (units/s^2).")]
    [SerializeField] private float steepSlideAcceleration = 25f;
    [Tooltip("Fastest the player slides down a too-steep slope (units/s).")]
    [SerializeField] private float steepSlideSpeed = 6f;


    //Wallcheck variables
    [Header("WallCheck")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.5f, 0.05f);
    [SerializeField] private LayerMask wallLayer;
    public bool isWalled {get; private set;} = false;
    private Collider2D[] wallCheckArray;
    private ContactFilter2D wallFilter;

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
    [Tooltip("Fall volume/pitch are remapped from this speed range up to maxFallSpeed")]
    [SerializeField] private float minFallSpeedForSound = 2f;
    [SerializeField] private float minFallVolume = 0.2f;
    [SerializeField] private float maxFallVolume = 1f;
    [SerializeField] private float minFallPitch = 1f;
    [SerializeField] private float maxFallPitch = 0.6f;
    private bool isFalling;

    //Fall animation buffer variables
    [Header("Fall Animation Buffer")]
    [Tooltip("How long the player must keep falling before the Fall animation is allowed to play. " +
             "Steps and tiny drops are over before this elapses, so they never trigger it.")]
    [SerializeField] private float fallAnimationDelay = 0.12f;
    [Tooltip("Falling faster than this plays the Fall animation right away, however briefly we've " +
             "been airborne (a real drop shouldn't wait out the buffer). 0 disables the shortcut.")]
    [SerializeField] private float fallAnimationSpeedThreshold = 8f;
    private float fallAnimTimer;    //Seconds spent descending in the air
    private bool fallAnimPlayed;    //True once Fall actually started — gates the Land animation

    //Jump animation buffer variables
    [Header("Jump Animation Buffer")]
    [Tooltip("How long the player must keep rising before the Jump animation is allowed to play. " +
             "Slope bumps and small shoves are over before this elapses, so they never trigger it.")]
    [SerializeField] private float jumpAnimationDelay = 0.08f;
    [Tooltip("Rising faster than this plays the Jump animation right away. A real jump launches at " +
             "jump_height, so keep this under half of it and taps still register instantly. " +
             "0 disables the shortcut.")]
    [SerializeField] private float jumpAnimationSpeedThreshold = 4f;
    private float jumpAnimTimer;    //Seconds spent rising in the air

    //Slingshot variables
    [Header("Slingshot")]
    [Tooltip("If ON, the player can steer left/right during a slingshot launch. " +
             "If OFF, horizontal control is locked until landing so the launch momentum is fully preserved.")]
    [SerializeField] private bool allowDirectionChangeWhileSlingshotting = false;
    public bool isSlingshotting { get; private set;} = false;
    private bool slingshotHasLeftGround = false;


    [Header("Swimming")]
    [SerializeField] float swimSpeed = 5;
    [SerializeField] float gravityUnderWater = -2f;
    [SerializeField] float underWaterDrag = 0f;
    [Tooltip("Upward speed given the moment the player leaves the water, so surfacing launches " +
             "them clear instead of stalling at the boundary. 0 disables the boost.")]
    [SerializeField] private float waterExitBoost = 8f;
    private float verticalMovement = 0f;
    public bool isSwimming { get; private set; }
    public bool IsSwimming() => isSwimming;

    void Awake()
    {
        if (animController == null) animController = GetComponent<AnimationController>();
       // if (audioController == null) audioController = GetComponent<AudioController>();

        groundFilter = new ContactFilter2D();
        groundContact = new ContactPoint2D[maxPoints];
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useLayerMask = true;
        groundFilter.SetNormalAngle(90f - maxSlopeAngle, 90f + maxSlopeAngle);
        groundFilter.useNormalAngle = true;

        // Same layers as the ground check but deliberately no normal filter, so this one can see
        // the steep contacts groundFilter throws away.
        surfaceFilter = new ContactFilter2D();
        surfaceFilter.SetLayerMask(groundLayer);
        surfaceFilter.useLayerMask = true;
        surfaceContact = new ContactPoint2D[maxPoints];

        wallCheckArray = new Collider2D[maxPoints];

        wallFilter = new ContactFilter2D();
        wallFilter.SetLayerMask(wallLayer);
        wallFilter.useLayerMask = true;
        wallFilter.useTriggers = true;
    }

    private void OnEnable()
    {
        WaterZone.PlayerSwimStateChanged += HandleSwimStateChanged;
    }

    private void OnDisable()
    {
        WaterZone.PlayerSwimStateChanged -= HandleSwimStateChanged;
    }

    // The water event is global, so ignore any raised for a different player.
    private void HandleSwimStateChanged(PlayerMovement player, bool swimming)
    {
        if (player != this) return;

        SetSwimming(swimming);
    }

    private void SetSwimming(bool swimming)
    {
        if (isSwimming == swimming) return;
        isSwimming = swimming;

        if (swimming)
        {
            // Hitting the water cancels every land-only movement state, otherwise a launch or
            // wall jump that carried us in would keep suppressing input under the surface.
            isSlingshotting = false;
            isWallJumping = false;
            isWallSliding = false;
            isWalled = false;
            isGrounded = false;
            wallJumpTimer = 0f;
        }
        else
        {
            // Back on land: drop the swim input so we don't keep pushing against gravity.
            verticalMovement = 0f;
            rb.gravityScale = baseGravity;

            // Pop clear of the surface. Only an upward exit is boosted, so dropping out of the
            // bottom of a water volume keeps its downward momentum instead of firing upward.
            // Mathf.Max means a fast ascent is never slowed down to the boost value.
            if (waterExitBoost > 0f && rb.linearVelocity.y > 0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, waterExitBoost));
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        ProcessWallJump();
        UpdateLocomotionAnimation();

        if (!isWallJumping && !(isSlingshotting && !allowDirectionChangeWhileSlingshotting)) //Prevent flipping during wall jump or a locked slingshot launch
        { 
            Flip(); //Flip player sprite based on movement direction
        }
            
    }

    // Picks the right locomotion animation from the current physics state each frame.
    // Uninterruptible animations (hurt/death) are left alone until they finish.
    private void UpdateLocomotionAnimation()
    {
        if (animController == null || animController.IsPlayingUninterruptible()) return;

        if (isSwimming)
        {
            // No dedicated swim clip yet, so reuse Run while paddling and Idle while drifting.
            // The ground/wall checks don't run underwater, so their flags can't be trusted here.
            bool isPaddling = Mathf.Abs(horizontalMovement) > 0.01f || Mathf.Abs(verticalMovement) > 0.01f;
            animController.PlayAnimation(isPaddling ? AnimationType.Run : AnimationType.Idle);

            // Sinking through water isn't falling, rising through it isn't jumping, and surfacing
            // shouldn't count as a landing.
            fallAnimTimer = 0f;
            jumpAnimTimer = 0f;
            fallAnimPlayed = false;
            return;
        }

        if (!isGrounded)
        {
            if (rb.linearVelocity.y > 0.01f)
            {
                // Still rising, so this isn't a fall yet — the buffer only counts descent time.
                fallAnimTimer = 0f;
                jumpAnimTimer += Time.deltaTime;

                bool risingLongEnough = jumpAnimTimer >= jumpAnimationDelay;
                bool risingFastEnough = jumpAnimationSpeedThreshold > 0f &&
                                        rb.linearVelocity.y >= jumpAnimationSpeedThreshold;

                if (risingLongEnough || risingFastEnough)
                {
                    animController.PlayAnimation(AnimationType.Jump);
                }

                // Same as the fall buffer: request nothing inside the window and the current
                // animation keeps running, so a bump up a slope doesn't blink into Jump.
                return;
            }

            jumpAnimTimer = 0f;
            fallAnimTimer += Time.deltaTime;

            bool fallingLongEnough = fallAnimTimer >= fallAnimationDelay;
            bool fallingFastEnough = fallAnimationSpeedThreshold > 0f &&
                                     -rb.linearVelocity.y >= fallAnimationSpeedThreshold;

            if (fallingLongEnough || fallingFastEnough)
            {
                // Requesting the same type every frame is a no-op, so Fall keeps looping for the
                // rest of the descent instead of restarting.
                animController.PlayAnimation(AnimationType.Fall);
                fallAnimPlayed = true;
            }

            // Inside the buffer window nothing is requested, so whatever was already playing keeps
            // running (Jump after a hop, Run/Idle after walking off a small step). That's the whole
            // point: a few airborne frames no longer flicker through Fall and Land.
            return;
        }

        fallAnimTimer = 0f;
        jumpAnimTimer = 0f;

        if (fallAnimPlayed)
        {
            // Landed after a fall long/fast enough to animate — small drops skip this too.
            animController.PlayAnimation(AnimationType.Land);
            fallAnimPlayed = false;
            DebugUtils.Log("Landed, playing landing animation");
        }
        else
        {
            animController.PlayAnimation(Mathf.Abs(horizontalMovement) > 0.01f ? AnimationType.Run : AnimationType.Idle);
        }
    }

    private void FixedUpdate()
    {
        if (isSwimming)
        {
            ApplyUnderWaterMovement();
            Gravity();
        }
        else
        {
            GroundCheck();
            SteepSlopeCheck();
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

            // Outside the branch above on purpose: the slide still has to run when the isWalled
            // case takes over and zeroes horizontal speed.
            ApplySteepSlopeSlide();

            Gravity();
            WallSlide();
        }
    }

    // Normal grounded/air horizontal movement with acceleration and friction.
    private void ApplyStandardMovement()
    {
        // Work off a copy: horizontalMovement is still read by Flip, WallSlide and the animation
        // code, which should keep seeing what the player is actually pressing.
        float input = horizontalMovement;

        // On a slope too steep to stand on, drop input that pushes uphill. Without this the solver
        // turns the horizontal push into motion along the surface and the player walks up a wall.
        // steepSlopeNormal.x points downhill, so -sign(normal.x) is the uphill direction.
        if (onSteepSlope && Mathf.Abs(input) > 0.01f &&
            Mathf.Sign(input) == -Mathf.Sign(steepSlopeNormal.x))
        {
            input = 0f;
        }

        if (Mathf.Abs(input) > 0.01f)
        {
            //Latch the flag first so a failed Play can't retrigger every FixedUpdate
            //if (!isWalking) {isWalking = true; audioController.Play(AudioType.Move);} //Remember to make audio loop
            float desiredX = input * moveSpeed;
            float accel = groundAcceleration; //Same acceleration whether on ground or in air
            float newX = Mathf.MoveTowards(rb.linearVelocity.x, desiredX, accel * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }
        else
        {
            float drag = isGrounded ? groundFriction : airDrag;
            float newX = Mathf.MoveTowards(rb.linearVelocity.x, 0f, drag * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
            isWalking = false;
        }
    }

    // Swimming steers on both axes at once, so each axis is solved separately and written
    // in a single assignment at the end. Input is signed, so it carries the swim direction.
    private void ApplyUnderWaterMovement()
    {
        float newX = Mathf.Abs(horizontalMovement) > 0.01f
            ? horizontalMovement * swimSpeed
            : Mathf.MoveTowards(rb.linearVelocity.x, 0f, underWaterDrag * Time.fixedDeltaTime);

        float newY = Mathf.Abs(verticalMovement) > 0.01f
            ? verticalMovement * swimSpeed
            : Mathf.MoveTowards(rb.linearVelocity.y, 0f, underWaterDrag * Time.fixedDeltaTime);

        // With no input on an axis the speed above is whatever physics left there, and nothing
        // else caps it underwater: buoyancy (a negative gravityUnderWater) accelerates upward
        // every frame, and momentum carried in from a slingshot or a fall never bleeds off when
        // underWaterDrag is 0. Either one would fire the player straight back out of the water,
        // so hold both axes to swim speed.
        rb.linearVelocity = new Vector2(
            Mathf.Clamp(newX, -swimSpeed, swimSpeed),
            Mathf.Clamp(newY, -swimSpeed, swimSpeed));
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
       // audioController.Play(AudioType.Slingshot);
    }

    //Different falling mechanics to make the game feel better. Increases fall speed the longer you fall, and caps it at a certain point.
    private void Gravity()
    {
      if (isSwimming)
        {
            // Water has one constant gravity (negative = buoyant). No fall acceleration, no
            // fall cap and no fall sound down here — ApplyUnderWaterMovement owns vertical speed.
            rb.gravityScale = gravityUnderWater;
            if (isFalling)
            {
                //audioController.StopLoop(AudioType.Fall);
                isFalling = false;
            }
            return;
        }

      if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = baseGravity * fallMultiplier; //Fall increasingly faster
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -maxFallSpeed)); //Cap fall speed

            float fallSpeed = -rb.linearVelocity.y;
            if (fallSpeed >= minFallSpeedForSound)
            {
                if (!isFalling)
                {
                    isFalling = true;
                    //audioController.StartLoop(AudioType.Fall);
                }

                float t = Mathf.InverseLerp(minFallSpeedForSound, maxFallSpeed, fallSpeed);
                //audioController.UpdateLoop(AudioType.Fall, Mathf.Lerp(minFallVolume, maxFallVolume, t), Mathf.Lerp(minFallPitch, maxFallPitch, t));
            }
        }
        else
        {
            rb.gravityScale = baseGravity; //Reset gravity when not falling
            if (isFalling)
            {
                //audioController.StopLoop(AudioType.Fall);
                isFalling = false;
            }
        }
    }

    private void WallSlide()
    {
        // Skip while sliding off a too-steep slope: the -wallSlideSpeed clamp below would stall it.
        if (isWalled && !onSteepSlope && horizontalMovement != 0)
        {
            isWallSliding = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -wallSlideSpeed)); //Limit fall speed while wall sliding
            //TODO: Play wallslide sound
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
            verticalMovement = 0;
         }
         else
         {
            // Directly set horizontal input each frame to feed acceleration logic
            Vector2 input = context.ReadValue<Vector2>();
            horizontalMovement = input.x;
            // Always cache vertical too — only swimming reads it, but caching it here means
            // entering the water with a direction already held starts paddling immediately.
            verticalMovement = input.y;
         }
    }

    public void Jump(InputAction.CallbackContext context)
    {

        //TODO: Remove w from jump keys
        if(context.performed && isWallSliding)
        {
            //audioController.Play(AudioType.Jump);
            isWallJumping = true;
            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpPower.x, wallJumpPower.y); //Jump away from the wall
            wallJumpTimer = wallJumpTime; //Reset wall jump timer

            //Force Flip
            if (transform.localScale.x != wallJumpDirection)
            {
                FlipMain();
            }
        }
        else if (context.performed && (isGrounded || isSwimming)) //hold jump = full jump power
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jump_height);
            //audioController.Play(AudioType.Jump);
        }
        else if (context.canceled && rb.linearVelocity.y >= 0 && !isSwimming) //if player taps rather than hold
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }   

        
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

    /// <summary>
    /// Looks for a contact too steep to stand on. groundFilter throws these away by design, so
    /// nothing downstream knew they existed — which is why the player could drive velocity.x into
    /// a steep drawn platform and let the solver slide them up it.
    /// </summary>
    private void SteepSlopeCheck()
    {
        onSteepSlope = false;
        if (isGrounded) return; // a walkable contact wins; never fight real ground

        int count = rb.GetContacts(surfaceFilter, surfaceContact);
        for (int i = 0; i < count; i++)
        {
            Vector2 normal = surfaceContact[i].normal;
            float angle = Vector2.Angle(normal, Vector2.up);

            // 90 and above is a wall or a ceiling, which is the wall check's job. Only real slopes here.
            if (angle > maxSlopeAngle && angle < 90f)
            {
                onSteepSlope = true;
                steepSlopeNormal = normal;
                return;
            }
        }
    }

    // Slide down a slope too steep to stand on instead of sticking to it under friction.
    private void ApplySteepSlopeSlide()
    {
        if (!onSteepSlope) return;

        // Descending tangent: rotate the normal 90 degrees, then pick whichever option points down.
        Vector2 downhill = new Vector2(steepSlopeNormal.y, -steepSlopeNormal.x);
        if (downhill.y > 0f) downhill = -downhill;

        // Accelerate along the surface up to steepSlideSpeed; Gravity() still caps outright fall speed.
        if (Vector2.Dot(rb.linearVelocity, downhill) < steepSlideSpeed)
        {
            rb.linearVelocity += downhill * steepSlideAcceleration * Time.fixedDeltaTime;
        }
    }

    private void WallCheck()
    {
        if (Physics2D.OverlapBox(wallCheck.position, wallCheckSize, 0f, wallFilter, wallCheckArray) > 0)
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
