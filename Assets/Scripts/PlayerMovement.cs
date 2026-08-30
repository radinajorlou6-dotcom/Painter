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

    //Coyote time and jump buffer variables
    [Header("Coyote Time & Jump Buffer")]
    [Tooltip("How long after leaving the ground the player can still jump. Covers a press that " +
             "lands just after walking off a ledge.")]
    [SerializeField] private float coyoteTime = 0.12f;
    [Tooltip("How long a jump press is remembered while airborne. If the player touches ground " +
             "within this window the jump fires on landing instead of being dropped.")]
    [SerializeField] private float jumpBufferTime = 0.12f;
    private float coyoteCounter;      //Seconds of ledge forgiveness left
    private float jumpBufferCounter;  //Seconds a pending jump press stays valid
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

    [Header("Climbing")]
    [Tooltip("How fast the player moves up and down a vine (units/s).")]
    [SerializeField] private float climbSpeed = 4f;
    [Tooltip("How long left or right has to be held before the player lets go of a vine. Covers " +
             "a stray tap, or a diagonal held on the way up, which would otherwise drop them off.")]
    [SerializeField] private float vineDismountHoldTime = 0.25f;
    [Tooltip("Sideways speed given when the player lets go by holding a direction, so they clear " +
             "the vine instead of dropping straight back into it.")]
    [SerializeField] private float vineDismountPush = 4f;
    [Tooltip("How far off the centre line of a vine the player can be and still take hold of it " +
             "(world units). Small values mean they have to line up with the vine properly " +
             "instead of catching its edge; too small and a vine is hard to grab in mid-air.")]
    [SerializeField] private float vineGrabTolerance = 0.25f;
    private VineZone currentVine;       //Unlocked vines we're overlapping — able to climb, not necessarily climbing
    private float vineColumnX;          //Centre line of the column being climbed; the player is held on it
    private bool vineDismounted;        //Let go on purpose: blocks re-grabbing until grounded or clear of the vine
    private float vineDismountCounter;  //Seconds a direction has been held while climbing
    private float vineDismountSign;     //Which side is being held, so switching sides restarts the hold
    public bool isClimbing { get; private set; }
    public bool IsClimbing() => isClimbing;

    void Awake()
    {
        if (animController == null) animController = GetComponent<AnimationController>();
        if (audioController == null) audioController = GetComponent<AudioController>();

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
        VineZone.PlayerVineStateChanged += HandleVineStateChanged;
    }

    private void OnDisable()
    {
        WaterZone.PlayerSwimStateChanged -= HandleSwimStateChanged;
        VineZone.PlayerVineStateChanged -= HandleVineStateChanged;
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
            SetClimbing(false);
            isSlingshotting = false;
            isWallJumping = false;
            isWallSliding = false;
            isWalled = false;
            isGrounded = false;
            wallJumpTimer = 0f;

            // Ledge forgiveness earned before the splash must not survive underwater.
            coyoteCounter = 0f;
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

    // The vine event is global, so ignore any raised for a different player.
    private void HandleVineStateChanged(PlayerMovement player, VineZone vine, bool onVine)
    {
        if (player != this) return;

        if (onVine)
        {
            currentVine = vine;
            return;
        }

        // Overlapping two patches at once, and the other one reported the exit: leave the one
        // we're actually on alone.
        if (currentVine != vine) return;

        currentVine = null;

        // Climbed off the end of the vine, or pushed clear of it. Vertical speed is left alone on
        // purpose, so climbing off the top carries the player over the lip instead of stalling
        // there and dropping straight back in.
        SetClimbing(false);

        // Being clear of the vine settles any deliberate let-go, so the next one can be grabbed
        // immediately.
        vineDismounted = false;
    }

    private void SetClimbing(bool climbing)
    {
        if (isClimbing == climbing) return;
        isClimbing = climbing;

        vineDismountCounter = 0f;
        vineDismountSign = 0f;

        if (climbing)
        {
            // Grabbing a vine cancels every land-only movement state, otherwise a launch or wall
            // jump that carried us into it would keep suppressing input all the way up.
            isSlingshotting = false;
            isWallJumping = false;
            isWallSliding = false;
            isWalled = false;
            isGrounded = false;
            wallJumpTimer = 0f;

            // Ledge forgiveness earned before the grab must not survive it. The jump buffer goes
            // too: a press made just before catching the vine would otherwise fire as a dismount
            // the very frame we arrive, which reads as the vine refusing to be grabbed.
            coyoteCounter = 0f;
            jumpBufferCounter = 0f;
        }
        else
        {
            rb.gravityScale = baseGravity;
        }
    }

    /// <summary>
    /// Deliberately lets go — a jump, or a direction held past the buffer. The vine can't be
    /// re-grabbed until the player either lands or moves clear of it, which is what stops a
    /// dismount inside a tall vine from being undone a few frames later by the auto-grab.
    /// </summary>
    private void ReleaseVine()
    {
        SetClimbing(false);
        vineDismounted = true;
    }

    /// <summary>
    /// Decides when the player takes hold of a vine and when they let go. Grabbing is automatic in
    /// the air, so jumping or falling into vines catches them; on the ground it waits for up or
    /// down, otherwise walking through the foot of a vine would snag the player every time.
    /// </summary>
    private void ProcessVineClimb()
    {
        // Landing settles a deliberate let-go even without leaving the vine, so a player standing
        // at the foot of one can climb it again instead of being locked out on the spot.
        if (isGrounded) vineDismounted = false;

        if (isClimbing)
        {
            ProcessVineDismount();
            return;
        }

        if (currentVine == null || vineDismounted || isSwimming) return;
        if (isGrounded && Mathf.Abs(verticalMovement) <= 0.01f) return;

        // Take hold only near the middle of a vine column. Touching the trigger is a much looser
        // test than it looks — it covers every vine on the tilemap at once, so without this the
        // player grabs on first contact and hangs off whichever edge they walked into.
        if (!currentVine.TryGetColumnCentre(rb.position, out float centreX)) return;
        if (Mathf.Abs(rb.position.x - centreX) > vineGrabTolerance) return;

        vineColumnX = centreX;
        SetClimbing(true);
        DebugUtils.Log($"Grabbed a vine at x {centreX}");
    }

    /// <summary>
    /// Runs the hold clock that drops the player off a vine. A direction has to be held for
    /// <see cref="vineDismountHoldTime"/> before it counts, so an accidental tap — or a diagonal
    /// held while climbing upward — doesn't shake them off.
    /// </summary>
    private void ProcessVineDismount()
    {
        float sign = Mathf.Abs(horizontalMovement) > 0.01f ? Mathf.Sign(horizontalMovement) : 0f;

        // Letting go of the key, or switching to the other side, restarts the hold from zero:
        // half a hold each way should never add up to a dismount.
        if (sign == 0f || sign != vineDismountSign)
        {
            vineDismountSign = sign;
            vineDismountCounter = 0f;
            return;
        }

        vineDismountCounter += Time.deltaTime;
        if (vineDismountCounter < vineDismountHoldTime) return;

        ReleaseVine();

        // Push off in the held direction. Without it the player accelerates out of a standstill
        // and is still inside the vine's trigger when the lockout ends, so they'd be caught again
        // before they ever got clear of it.
        rb.linearVelocity = new Vector2(sign * vineDismountPush, rb.linearVelocity.y);
        DebugUtils.Log("Let go of the vine");
    }

    // Update is called once per frame
    void Update()
    {
        // Before the jump timers: grabbing a vine spends the jump buffer, so a press that arrived
        // on the way in can't fire as a dismount on the frame the player takes hold.
        ProcessVineClimb();
        ProcessWallJump();
        ProcessJumpTimers();
        UpdateLocomotionAnimation();

        if (!isWallJumping && !isClimbing && !(isSlingshotting && !allowDirectionChangeWhileSlingshotting)) //Prevent flipping during wall jump, a climb, or a locked slingshot launch
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

        if (isClimbing)
        {
            // No dedicated climb clip yet, so reuse Run while moving along the vine and Idle while
            // hanging still. The ground and wall checks don't run while climbing, so their flags
            // can't be trusted here.
            animController.PlayAnimation(Mathf.Abs(verticalMovement) > 0.01f ? AnimationType.Run : AnimationType.Idle);

            // Riding a vine down isn't falling, riding it up isn't jumping, and letting go of one
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
            TutorialManager.Report(TutorialSignal.Landed);
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
        else if (isClimbing)
        {
            ApplyClimbMovement();
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

    // Vertical speed comes straight from input, and the player is pinned horizontally: sideways
    // input on a vine is a dismount request, not movement, so it must not slide them off the vine
    // while the hold clock is still running.
    private void ApplyClimbMovement()
    {
        float newY = Mathf.Abs(verticalMovement) > 0.01f ? verticalMovement * climbSpeed : 0f;
        rb.linearVelocity = new Vector2(0f, newY);

        // Held on the column's centre line rather than merely stopped: the grab is allowed from
        // anywhere inside vineGrabTolerance, so this is what actually centres the player on the
        // vine, and it keeps them there if anything shoves them sideways afterwards.
        rb.position = new Vector2(vineColumnX, rb.position.y);
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
        TutorialManager.Report(TutorialSignal.Slingshotted);
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
                audioController.StopLoop(AudioType.Fall);
                isFalling = false;
            }
            return;
        }

      if (isClimbing)
        {
            // Hanging on a vine: ApplyClimbMovement owns vertical speed, so gravity has to be off
            // entirely rather than fought frame by frame. Nothing on a vine counts as falling.
            rb.gravityScale = 0f;
            if (isFalling)
            {
                audioController.StopLoop(AudioType.Fall);
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
                    audioController.StartLoop(AudioType.Fall);
                }

                float t = Mathf.InverseLerp(minFallSpeedForSound, maxFallSpeed, fallSpeed);
                audioController.UpdateLoop(AudioType.Fall, Mathf.Lerp(minFallVolume, maxFallVolume, t), Mathf.Lerp(minFallPitch, maxFallPitch, t));
            }
        }
        else
        {
            rb.gravityScale = baseGravity; //Reset gravity when not falling
            if (isFalling)
            {
                audioController.StopLoop(AudioType.Fall);
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
            audioController.Play(AudioType.Jump);
            isWallJumping = true;
            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpPower.x, wallJumpPower.y); //Jump away from the wall
            wallJumpTimer = wallJumpTime; //Reset wall jump timer

            //Force Flip
            if (transform.localScale.x != wallJumpDirection)
            {
                FlipMain();
            }
        }
        else if (context.performed)
        {
            // Not a wall jump: only remember the press. ProcessJumpTimers fires it once the
            // player has footing, which may be a few frames from now (buffer) or a few frames
            // after leaving the ledge (coyote).
            jumpBufferCounter = jumpBufferTime;
        }
        else if (context.canceled && rb.linearVelocity.y >= 0 && !isSwimming && !isClimbing) //if player taps rather than hold
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }


    }

    /// <summary>
    /// Runs the coyote and jump-buffer clocks and fires a remembered jump the moment the player
    /// has footing. Both windows exist so a press that's slightly early or slightly late still
    /// produces the jump the player expected instead of being silently dropped.
    /// </summary>
    private void ProcessJumpTimers()
    {
        // Refill only while actually resting on the ground. The velocity gate matters: for a
        // frame or two after a jump the feet are still touching, and an unconditional refill
        // there would hand back a full coyote window mid-launch — a free double jump.
        if (isGrounded && rb.linearVelocity.y <= 0.01f) coyoteCounter = coyoteTime;
        else coyoteCounter -= Time.deltaTime;

        if (jumpBufferCounter > 0f) jumpBufferCounter -= Time.deltaTime;

        // Swimming keeps its existing always-allowed upward burst, and a vine is footing of its
        // own — the player can always push off one.
        if (jumpBufferCounter > 0f && (coyoteCounter > 0f || isSwimming || isClimbing))
        {
            PerformGroundJump();
        }
    }

    private void PerformGroundJump()
    {
        // Jumping off a vine is a deliberate let-go, so it locks the vine out exactly the way
        // holding a direction does — otherwise the auto-grab would catch the player again on the
        // way up and the jump would look like it did nothing.
        if (isClimbing) ReleaseVine();

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jump_height);

        // Spend both timers so a single press can never produce two jumps.
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        audioController.Play(AudioType.Jump);
        TutorialManager.Report(TutorialSignal.Jumped);
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
