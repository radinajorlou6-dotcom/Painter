using System.Collections; // Required for Coroutines
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;

    [Header("Ranged Attack")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float fireRate = 0.5f;
    private float nextFireTime = 0f; // Changed to track the actual game time

    [Header("Melee Attack")]
    [SerializeField] private Transform weaponPoint;
    [SerializeField] private PolygonCollider2D hitBox;
    [SerializeField] private float meleeDuration = 0.2f;
    [SerializeField] private float dmgMult = 1f;
    [SerializeField] private float slashRadius = 3f;
    [SerializeField] private float maxSweepAngle = 180f;
    [SerializeField] private int arcResolution = 15; // How many points to use for the curve (higher = smoother but more expensive)
    [SerializeField] private LayerMask enemyLayers;

    [Header("Shield")]
    [SerializeField] private GameObject shieldPrefab;
    public static bool isShieldActive = false;
    // ... (Your shield variables remain untouched)

    void Update()
    {
        // Gluing the floating pivot to the player's position
        if (playerTransform != null)
        {
            transform.position = playerTransform.position;
        }
    }

    public void RangedAttack(Vector2 targetWorldPos)
    {
        // 1. FIXED COOLDOWN LOGIC: Check against the actual game clock
        if (Time.time < nextFireTime) return;

        Debug.Log("Ranged Attack fired towards: " + targetWorldPos);

        // 2. FIXED DOUBLE-MATH: targetWorldPos is already correct, just subtract firePoint!
        Vector2 direction = (targetWorldPos - (Vector2)firePoint.position).normalized;

        GameObject bullet = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction * projectileSpeed;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Set the clock for when we are allowed to fire next
        nextFireTime = Time.time + fireRate;
    }

    public void ExecuteDynamicSlash(List<Vector2> swipePath)
    {
        if (swipePath == null || swipePath.Count < 2) return;

        // --- PHASE 1: ANGLE CALCULATION (The Brain) ---
        Vector2 startDirection = swipePath[0] - (Vector2)transform.position;
        float startAngle = Mathf.Atan2(startDirection.y, startDirection.x) * Mathf.Rad2Deg;

        float previousAngle = startAngle;
        float accumulatedAngle = 0f;
        float swingSign = 0f;

        foreach (Vector2 point in swipePath)
        {
            Vector2 direction = point - (Vector2)transform.position;

            

            float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float step = Mathf.DeltaAngle(previousAngle, currentAngle);

            // Figure out swing direction
            if (swingSign == 0f && Mathf.Abs(step) > 0.01f)
            {
                swingSign = Mathf.Sign(step);
            }
            // NO BACKTRACKING RULE
            else if (swingSign != 0f && Mathf.Sign(step) != swingSign)
            {
                continue;
            }

            accumulatedAngle += Mathf.Abs(step);

            // CLAMP: Stop counting if we hit a half-circle
            if (accumulatedAngle >= maxSweepAngle)
            {
                accumulatedAngle = maxSweepAngle;
                break;
            }

            previousAngle = currentAngle;
        }

        // --- PHASE 2: SHAPE GENERATION ---
        List<Vector2> polygonPoints = new List<Vector2>();
        polygonPoints.Add(Vector2.zero); // Center of the player

        // Calculate the exact final angle based on our running total
        float finalAngle = startAngle + (accumulatedAngle * swingSign);

        // Draw a mathematically perfect, smooth curve between the Start and Final angles
        for (int i = 0; i <= arcResolution; i++)
        {
            float t = i / (float)arcResolution; // Percentage of the curve (0.0 to 1.0)

            float angleDeg = Mathf.Lerp(startAngle, finalAngle, t);
            float angleRad = angleDeg * Mathf.Deg2Rad;

            Vector2 curvePoint = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * slashRadius;
            polygonPoints.Add(curvePoint);
        }

        // Apply the perfect shape and start the attack!
        hitBox.SetPath(0, polygonPoints.ToArray());
        StartCoroutine(MeleeAttackRoutine());
    }

    private IEnumerator MeleeAttackRoutine()
    {
        // Turn the visual/physics shape on
        hitBox.gameObject.SetActive(true);

        // Create a filter to tell Unity exactly what layer to look for
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayers);
        filter.useTriggers = true; // Set to false if your enemies use physical colliders instead of triggers

        List<Collider2D> hitEnemies = new List<Collider2D>();

        // Grab everything currently touching our custom Polygon Collider
        Physics2D.OverlapCollider(hitBox, filter, hitEnemies);

        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(10f * dmgMult); 
            }
            Debug.Log("Hit enemy: " + enemy.name);
        }

        // Wait for a split second so the player can actually see the slash
        yield return new WaitForSeconds(meleeDuration);

        // Turn the hitbox back off
        hitBox.gameObject.SetActive(false);
    }

    public void ToggleShield(bool isActive)
    {
        isShieldActive = isActive;
        if (isActive) { /* Spawn */ }
        else { /* Stop */ }
    }
}