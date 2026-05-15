using System.Collections;
using System.Collections.Generic;
using UnityEditor.Tilemaps;
using UnityEngine;

public class KnightAI : EnemyHealth
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float moveToPlayerSpeed = 5f;

    [Header("Melee Attack")]
    [SerializeField] protected float knockbackDuration = 0.2f;
    [SerializeField] protected Transform weaponPoint;
    [SerializeField] protected CircleCollider2D hitBox;
    [SerializeField] protected float bashDuration = 0.2f;
    [SerializeField] protected float bashCooldown = 0f; //INCASE WE WANT MELEE COOLDOWN TO BE DIFFERENT FROM MELEE ANIMATION LENGTH
    [SerializeField] protected float bashRange = 2f;
    [SerializeField] protected float chargeDmg = 20f;
    [SerializeField] protected float chargeKnockback = 5f;
    [SerializeField] protected float bashDmg = 5f;
    [SerializeField] protected float bashKnockback = 15f;
    [SerializeField] protected float slashRadius = 3f;
    [SerializeField] protected float slashKnockback = 1f;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        
    }

    // Update is called once per frame
    protected override void Update()
    {
        if (canSeePlayer)
        {
            AttackMove();
        }
        else
        {
            Move();
        }
        base.Update();
    }

    private void Move()
    {
        if (dirIsRight)
        {
            transform.Translate(Vector2.right * Time.deltaTime * moveSpeed);
        }
        else
        {
            transform.Translate(Vector2.left * Time.deltaTime * moveSpeed);
        }
    }

    private void AttackMove()
    {
        if (currDistanceFromPlayer <= bashRange)
        {
            BashAttack();
        }
    }

    protected override void BaseAttack()
    {
        throw new System.NotImplementedException();
    }

    private IEnumerator BashAttack()
    {
        // Turn the visual/physics shape on
        hitBox.gameObject.SetActive(true);

        // Wait one frame so the physics engine has a chance to register the enabled collider
        yield return null;

        // Create a filter to tell Unity exactly what layer to look for
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(playerLayer);
        filter.useTriggers = true; ; // Set to false if your enemies use physical colliders instead of triggers

        Collider2D hit = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
        if (hit != null)
        {
            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
            playerHealth.TakeDamage(bashDmg);
            Vector2 knockbackDir = (player.transform.position - transform.position).normalized;
            StartCoroutine(playerHealth.TakeKnockback(knockbackDir, bashKnockback, knockbackDuration));
            yield return new WaitForSeconds(bashDuration);
        }
        

        // Turn the hitbox back off
        hitBox.gameObject.SetActive(false);
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
