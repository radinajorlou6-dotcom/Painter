    using System.Collections;
    using UnityEngine;

    public class KnightAI : EnemyBase
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float chargeSpeed = 5f;
        
        [Header("Animation")]
        [SerializeField] private AnimationController animController;

        [Header("Melee Attack")]
        [SerializeField] protected float knockbackDuration = 0.2f;
        [SerializeField] protected Transform weaponPoint;
        [SerializeField] protected BoxCollider2D hitBox;
        [SerializeField] protected float bashDuration = 0.2f;
        [SerializeField] protected float bashCooldown = 0f; //INCASE WE WANT MELEE COOLDOWN TO BE DIFFERENT FROM MELEE ANIMATION LENGTH
        [SerializeField] protected float bashRange = 2f;
        [SerializeField] protected float bashDmg = 5f;
        [SerializeField] protected float bashKnockback = 15f;
        [SerializeField] protected float chargeDmg = 20f;
        [SerializeField] protected float chargeKnockback = 5f;
        [SerializeField] protected float chargeDuration = 1f;
        [SerializeField] protected float chargeCooldown = 0f;


        private bool isAttacking = false;

        protected override void Awake()
        {
            base.Awake();
            // The knight is armoured: it shrugs off the player's attacks. We express
            // this through the Health component instead of overriding TakeDamage to
            // do nothing, so it still honours the IHealth/IDamageable contract.
            // Hazards can still finish it off via Health.Kill().
            health.Invulnerable = true;
            if (animController == null) animController = GetComponent<AnimationController>();

        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();
            //Debug.Log("Is attacking: " + isAttacking);
            if (isAttacking) return; // Prevent moving or starting new attacks while already attacking

            if (canSeePlayer)
            {
                //Debug.Log("Going into attackMove");
                AttackMove();
            }
            else
            {
                Move();
            }

            if (!HasFooting)
            {
                animController?.PlayAnimation(AnimationType.Idle);
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Stop horizontal movement when falling
            }
        }

        private void Move()
        {
            if (isBeingKnocked) return; // Do not steer while being knocked back

            // A genuine fall: stop steering, but stop drifting too. This used to test isGrounded
            // and return with last frame's velocity untouched, so the moment the ground check
            // flickered — a tile seam, a slope, the bounce after a landing — the knight kept its
            // full walking speed with no ledge check running. One flickered frame beside a drop
            // was all it took to coast straight over the edge.
            if (!HasFooting)
            {
                Halt();
                animController?.PlayAnimation(AnimationType.Idle);
                return;
            }

            // Turn around at a ledge, spikes, or a wall ahead — instead of grinding in place.
            if (!IsGroundAhead() || IsSpikeAhead() || isColliding)
            {
                Halt(); // kill momentum before turning, or we slide over the ledge we just spotted
                Flip();
                animController?.PlayAnimation(AnimationType.Idle); // not actually moving this frame
                return;
            }

            float dir = dirIsRight ? 1f : -1f;
            // Do not use Time.deltaTime when setting linearVelocity directly
            rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);
            animController?.PlayAnimation(AnimationType.Walk);
        }

        /// <summary>Cancel horizontal drift while leaving gravity's pull intact.</summary>
        private void Halt()
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        private bool IsSpikeAhead()
        {
            // Expand the detection area so it catches spikes on the ground and on walls ahead of the knight.
            Vector2 checkPos = new Vector2(transform.position.x + (dirIsRight ? edgeCheckDistance : -edgeCheckDistance), transform.position.y);
            Vector2 spikeCheckSize = new Vector2(edgeCheckDistance + groundCheckSize.x, groundCheckSize.y + collideCheckSize.y + 0.5f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(checkPos, spikeCheckSize, 0f);
            foreach (Collider2D hit in hits)
            {
                if (hit != null && hit.CompareTag(Health.HazardTag))
                {
                    return true;
                }
            }
            return false;
        }

        private void AttackMove()
        {
            if (isAttacking) return;
            else if (currDistanceFromPlayer <= bashRange)
            {
                StartCoroutine(BashAttack());
            }
            else
            {
                StartCoroutine(BaseAttack());
            }
            
        }

        protected override void CheckPlayerDetection()
        {
            if (player == null) return;
            currDistanceFromPlayer = Vector2.Distance(transform.position, player.position); //Check how far the player is
            RaycastHit2D seePlayer = Physics2D.Linecast(transform.position, player.position, environment); //Check if theres anything in the way
            //Debug.Log("Here");
            if (currDistanceFromPlayer <= detectionRange && seePlayer.collider == null) //if player is within detection range and theres nothing in the way
            {
                // Determine if the player is to the left or right of us
                bool playerOnRight = player.position.x > transform.position.x;

                // Check if the player is on the side we are actually facing
                canSeePlayer = (playerOnRight == dirIsRight);
            }
            else
            {
                canSeePlayer = false;
            }
        }

        protected override IEnumerator BaseAttack()
        {
            isAttacking = true;
            animController?.PlayAnimation(AnimationType.Charge);
            hitBox.gameObject.SetActive(true); // Enable ONCE before the loop
        
            // Determine charge direction and set velocity ONCE
            float chargeDir = dirIsRight ? 1f : -1f;
            rb.linearVelocity = new Vector2(chargeDir * chargeSpeed, rb.linearVelocity.y);

            yield return null;

            // Charge until we hit a wall, hit the player, OR the charge time runs out.
            // The time cap is essential: without it, a charge into open space (or off a
            // ledge) never sets isColliding, so the loop — and isAttacking — would hang forever.
            float elapsed = 0f;
            while (!isColliding && elapsed < chargeDuration)
            {
                Vector2 hitCenter = hitBox.bounds.center;
                float hitRadius = hitBox.bounds.extents.x;
                Collider2D hit = Physics2D.OverlapCircle(hitCenter, hitRadius, playerLayer);
                if (hit != null)
                {
                    animController?.PlayAnimation(AnimationType.Bash);
                    ApplyHit(hit, chargeDmg, chargeKnockback);
                    break; // Stop charging once we hit the player
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Clean up after charge ends (hit wall, hit player, or timed out)
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            hitBox.gameObject.SetActive(false); // Disable ONCE after the loop

            yield return new WaitForSeconds(chargeCooldown);
            isAttacking = false;
        }

        private IEnumerator BashAttack()
        {
            isAttacking = true;

            // Turn the visual/physics shape on
            hitBox.gameObject.SetActive(true);

            // Wait one frame so the physics engine has a chance to register the enabled collider
            yield return null;

            // Use the hitBox's actual world center and radius for accuracy
            Vector2 hitCenter = hitBox.bounds.center;
            float hitRadius = hitBox.bounds.extents.x;
            Collider2D hit = Physics2D.OverlapCircle(hitCenter, hitRadius, playerLayer);
            
            //Debug.Log("Bash hit: " + (hit != null));
            if (hit != null)
            {
                ApplyHit(hit, bashDmg, bashKnockback);
            }

            yield return new WaitForSeconds(bashDuration);

            // Turn the hitbox back off
            hitBox.gameObject.SetActive(false);

            // Apply cooldown before allowing another attack
            yield return new WaitForSeconds(bashCooldown);
            isAttacking = false;
        }

        /// <summary>
        /// Applies damage and knockback to whatever was hit, using the health
        /// interfaces so we never care about the concrete target type.
        /// </summary>
        private void ApplyHit(Collider2D hit, float damage, float knockbackForce)
        {

            animController?.PlayAnimation(AnimationType.Bash);
            IDamageable target = hit.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }

            IKnockbackable knockable = hit.GetComponent<IKnockbackable>();
            if (knockable != null)
            {
                Vector2 knockbackDir = (player.position - transform.position).normalized;
                StartCoroutine(knockable.TakeKnockback(knockbackDir, knockbackForce, knockbackDuration));
            }
        }

        // No OnCollisionEnter2D here any more. Spikes are handled by Health for every entity that
        // has one, and the wall turn was a duplicate: it only fired when isColliding was already
        // true, which is exactly when Move() flips anyway. Two flips in one frame turned the
        // knight back into the ledge it had just refused, and it flipped mid-charge as well.
    }
