using System.Collections.Specialized;
using UnityEngine;

public class Bullet : MonoBehaviour
{

    [SerializeField] private float damage = 10f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Bullet collided with: " + collision.gameObject.name);
        /*
        // Check if the bullet collides with an enemy
        if (collision.CompareTag("Enemy"))
        {
            // Apply damage to the enemy
            EnemyHealth enemyHealth = collision.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
            // Destroy the bullet after hitting an enemy
        }
              */  //YET TO BE IMPLEMENTED MAKE ENEMY FIRST

        if (!collision.CompareTag("Player"))
        {
            Debug.Log("Bullet hit something that is not the player, destroying bullet.");
            Destroy(gameObject);
        }
       
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Destroy(gameObject, 5f); // Destroy the bullet after 5 seconds if it doesn't hit anything
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
