using UnityEditor.Tilemaps;
using UnityEngine;

public class KnightAI : EnemyHealth
{
    [SerializeField] private float moveSpeed = 5f;

    [Header("Melee Attack")]
    [SerializeField] protected float knockbackDuration = 0.2f;
    [SerializeField] protected Transform weaponPoint;
    [SerializeField] protected PolygonCollider2D hitBox;
    [SerializeField] protected float meleeDuration = 0.2f;
    [SerializeField] protected float meleeCooldown = 0f; //INCASE WE WANT MELEE COOLDOWN TO BE DIFFERENT FROM MELEE ANIMATION LENGTH
    [SerializeField] protected float dmgMult = 1f;
    [SerializeField] protected float slashRadius = 3f;
    [SerializeField] protected float maxSweepAngle = 180f;
    [SerializeField] protected int arcResolution = 15; // How many points to use for the curve (higher = smoother but more expensive)
    [SerializeField] protected float slashKnockback = 1f;
    [SerializeField] protected LayerMask enemyLayers;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        
    }

    // Update is called once per frame
    protected override void Update()
    {
        Move();
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
