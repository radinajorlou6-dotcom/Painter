    using System.Collections;
    using System.Collections.Generic;
    using UnityEditor.Tilemaps;
    using UnityEngine;

    public class KnightAI : EnemyHealth
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float chargeSpeed = 5f;

        [Header("Edge Detection")]
        [SerializeField] private float edgeCheckDistance = 0.5f; // How far ahead to check for ground

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

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override void Start()
        {
            base.Start();
        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();
            Debug.Log("Is attacking: " + isAttacking);
            if (isAttacking) return; // Prevent moving or starting new attacks while already attacking

            if (canSeePlayer)
            {
                Debug.Log("Going into attackMove");
                AttackMove();
            }
            else
            {
                Move();
            }
        }

        private void Move()
        {
            if (!isGrounded || isBeingKnocked) return; // Do not move if we're in the air or being knocked back
            
            // Check if there's ground ahead or a spike in front when patrolling
            if (!IsGroundAhead() || IsSpikeAhead())
            {
                Flip(); // Turn around if walking off an edge or into spikes
                return;
            }
            
            if (dirIsRight)
            {
                // Do not use Time.deltaTime when setting linearVelocity directly
                rb.linearVelocity = new Vector2(moveSpeed, rb.linearVelocity.y);
            }
            else
            {
                rb.linearVelocity = new Vector2(-moveSpeed, rb.linearVelocity.y);
            }
        }

        private bool IsGroundAhead()
        {
            // Check in front of the knight at ground level
            Vector2 checkPos = new Vector2(transform.position.x + (dirIsRight ? edgeCheckDistance : -edgeCheckDistance), groundCheck.position.y);
            return Physics2D.OverlapBox(checkPos, groundCheckSize, 0, groundLayer);
        }

        private bool IsSpikeAhead()
        {
            // Expand the detection area so it catches spikes on the ground and on walls ahead of the knight.
            Vector2 checkPos = new Vector2(transform.position.x + (dirIsRight ? edgeCheckDistance : -edgeCheckDistance), transform.position.y);
            Vector2 spikeCheckSize = new Vector2(edgeCheckDistance + groundCheckSize.x, groundCheckSize.y + collideCheckSize.y + 0.5f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(checkPos, spikeCheckSize, 0f);
            foreach (Collider2D hit in hits)
            {
                if (hit != null && hit.CompareTag("Spikes"))
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
            Debug.Log("Here");
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
            hitBox.gameObject.SetActive(true); // Enable ONCE before the loop
        
            // Determine charge direction and set velocity ONCE
            float chargeDir = dirIsRight ? 1f : -1f;
            rb.linearVelocity = new Vector2(chargeDir * chargeSpeed, rb.linearVelocity.y);

            yield return null;

            while (!isColliding)
            {
                Vector2 hitCenter = hitBox.bounds.center;
                float hitRadius = hitBox.bounds.extents.x;
                Collider2D hit = Physics2D.OverlapCircle(hitCenter, hitRadius, playerLayer);
                Debug.Log("WE GOT HIM " + (hit != null));
                if (hit != null)
                {
                    PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(chargeDmg);
                        Vector2 knockbackDir = (player.position - transform.position).normalized;
                        StartCoroutine(playerHealth.TakeKnockback(knockbackDir, chargeKnockback, knockbackDuration));
                    }
                    break; // Stop charging once we hit the player
                }
                yield return null;
            }

            // Clean up after charge ends (hit wall or hit player)
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            hitBox.gameObject.SetActive(false); // Disable ONCE after the loop
        
            yield return new WaitForSeconds(chargeDuration);
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
            
            Debug.Log("Bash hit: " + (hit != null));
            if (hit != null)
            {
                PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(bashDmg);
                    Vector2 knockbackDir = (player.position - transform.position).normalized;
                    StartCoroutine(playerHealth.TakeKnockback(knockbackDir, bashKnockback, knockbackDuration));
                }
            }

            yield return new WaitForSeconds(bashDuration);

            // Turn the hitbox back off
            hitBox.gameObject.SetActive(false);

            // Apply cooldown before allowing another attack
            yield return new WaitForSeconds(bashCooldown);
            isAttacking = false;
        }

        public override void TakeDamage(float damage)
        {
            //This enemy does not take damage from player
            //PLAY ANIMATION
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Spikes"))
            {
                Die();
            }
            else if ((collideWithLayer.value & (1 << collision.gameObject.layer)) != 0
                    && isColliding) //Check if the collision is something that should make the knight turn around
            {
                Flip();
            }
        }
    }
