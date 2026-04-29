using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class PlayerCombat : MonoBehaviour
{

    [SerializeField] private Transform playerTransform; //To follow the player around with the pivot point for attacks, assign in inspector

    //Ranged attack variables
    [Header("Ranged Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float fireRate = 0.5f;
    [SerializeField] private float nextFireTime = 0f;

    //Melee attack variables
    [Header("Mellee Attack")]
    [SerializeField] private Transform weaponPoint;
    [SerializeField] private GameObject hitBox;
    [SerializeField] private float maxDragDistance = 5f;
    [SerializeField] private float minHitBoxSize = 0.2f;
    [SerializeField] private float meleeDuration = 0.2f;
    [SerializeField] private float dmgMult = 1f;
    [SerializeField] private float meleeRange = 1f;
    [SerializeField] private LayerMask enemyLayers;

    //Shield variables
    [Header("Shield")]
    [SerializeField] private GameObject shieldPrefab;
    public static bool isShieldActive = false;
    private GameObject currShield;
    private LineRenderer currLine;
    private EdgeCollider2D currCollider;
    private List<Vector2> shieldPoints;
    private float minPointDistance = 0.1f;
    [SerializeField] private float shieldDuration = 5f;

    public void RangedAttack(Vector2 target)
    {
        if (nextFireTime > 0)
        {
            nextFireTime -= Time.deltaTime;
            return;
        }
        Debug.Log("Ranged Attack function called with target: " + target);

        Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(new Vector3(target.x, target.y, 10f));
        worldMousePos.z = 0f; // Ensure the z-coordinate is zero for 2D

        Vector2 direction = (worldMousePos - firePoint.position).normalized;

        GameObject bullet = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity); //Spawn projectile at firePoint

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction * projectileSpeed;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        nextFireTime = fireRate; //Reset fire timer

    }

    public void PerformMelee(Vector2 attackDir)
    {
        float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg; //The angle we want the hitbox to turn
        weaponPoint.rotation = Quaternion.Euler(0, 0, angle); //Rotate the hitbox to face the attack direction
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(weaponPoint.position, meleeRange, enemyLayers); //Detect enemies in range
        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(10f * dmgMult); //Apply damage to enemies
            }
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Gluing the pivot to the players position
        if (playerTransform  != null)
        {
            transform.position = playerTransform.position;
        }
    }

    public void ToggleShield(bool isActive)
    {
        isShieldActive = isActive;
        if (isActive)
        {
            // Logic to spawn the Shield Prefab goes here
        }
        else
        {
            // Logic to stop drawing goes here
        }
    }
}
