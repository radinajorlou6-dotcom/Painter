using UnityEngine;

//FOR NOW EXACT SAME AS ENEMY HEALTH

public class PlayerHealth : MonoBehaviour
{

    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float health;
    [SerializeField] private Animator anim;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        health = maxHealth;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TakeDamage(float damage)
    {
        anim.SetTrigger("gotHurt");
        health -= damage;
        Debug.Log(gameObject.name + " took " + damage + " damage. Remaining health: " + health);
        if (health <= 0)
        {
            anim.SetTrigger("Died");
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            GetComponent<PlayerMovement>().enabled = false;
            this.enabled = false;
        }
    }

    private void Die()
    {
        Debug.Log(gameObject.name + " has died.");
        Destroy(gameObject);
    }
}
