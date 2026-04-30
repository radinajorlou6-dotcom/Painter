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

    [Header("Shield Drawing Settings")]
    [SerializeField] private LineRenderer shieldLine;
    [SerializeField] private EdgeCollider2D shieldCollider;
    [SerializeField] private float maxShieldRadius = 3f;
    [SerializeField] private float minShieldRadius = 0.5f;
    [SerializeField] private float minPointDistance = 0.2f;
    [Tooltip("The total physical length of the line the player is allowed to draw")]
    [SerializeField] private float maxPaintAmount = 10f;
    private struct ShieldNode
    {
        public Vector2 localPosition;
        public float birthTime;
    }

    [Header("Shield Durability Settings")]
    [Tooltip("How long the shield lasts after letting go")]
    [SerializeField] private float shieldDuration = 1f;
    [Tooltip("How many hits the shield can take before breaking")]
    [SerializeField] private int maxShieldHits = 1;

    // Internal State
    private List<ShieldNode> shieldNodes = new List<ShieldNode>();
    private float currentPaintUsed = 0f;
    private bool outOfPaint = false;
    private int currentHitsRemaining;
    private Coroutine shieldTimerRoutine;


    #region ShieldLogic
    public void StartNewShield()
    {
        // Cancel any active self-destruct timers from a previous shield
        if (shieldTimerRoutine != null) StopCoroutine(shieldTimerRoutine);

        shieldNodes.Clear();
        currentPaintUsed = 0f;
        outOfPaint = false;

        shieldLine.positionCount = 0;
        shieldCollider.points = new Vector2[0];

        shieldLine.enabled = true;
        shieldCollider.enabled = true;
    }

    public void AddShieldPoint(Vector2 worldPos)
    {
        if (outOfPaint) return; // Stop drawing if the paint tube is empty

        Vector2 offset = worldPos - (Vector2)transform.position;
        float distanceFromCenter = offset.magnitude;

        // THE DEADZONE: Ignore points too close to the center
        if (distanceFromCenter < minShieldRadius) return;

        // CLAMP TO RADIUS: Pull the point to the edge of the bubble if it's too far
        if (distanceFromCenter > maxShieldRadius)
        {
            offset = offset.normalized * maxShieldRadius;
        }

        if (shieldNodes.Count == 0)
        {
            shieldNodes.Add(new ShieldNode { localPosition = offset, birthTime = Time.time });
            UpdateShieldVisuals();
            return;
        }

        // Calculate the physical length of this new line segment
        float segmentLength = Vector2.Distance(shieldNodes[shieldNodes.Count - 1].localPosition, offset);

        if (segmentLength > minPointDistance)
        {
            // THE PAINT LIMIT: Check if adding this line would exceed our total ink
            if (currentPaintUsed + segmentLength > maxPaintAmount)
            {
                outOfPaint = true; // Lock the shield
                return;
            }

            // Consume the paint and add the point!
            currentPaintUsed += segmentLength;

            ShieldNode newNode = new ShieldNode
            {
                localPosition = offset,
                birthTime = Time.time,
            };
            shieldNodes.Add(newNode);
            UpdateShieldVisuals();
        }
    }

    public void DeployShield()
    {
        // The player let go! Arm the shield with health and start the countdown.
        currentHitsRemaining = maxShieldHits;
        shieldTimerRoutine = StartCoroutine(ShieldCountdown());
    }

    private IEnumerator ShieldCountdown()
    {
        // Wait for the editable duration
        yield return new WaitForSeconds(shieldDuration);
        BreakShield();
    }

    // Call this from your Enemy or Projectile script when they hit the shield!
    public void TakeShieldHit()
    {
        currentHitsRemaining--;
        if (currentHitsRemaining <= 0)
        {
            BreakShield();
        }
    }

    private void BreakShield()
    {
        // Turn the visuals and physics off
        shieldLine.enabled = false;
        shieldCollider.enabled = false;
    }

    private void UpdateShieldVisuals()
    {
        shieldLine.positionCount = shieldNodes.Count;
        for (int i = 0; i < shieldNodes.Count; i++)
        {
            shieldLine.SetPosition(i, new Vector3(shieldNodes[i].localPosition.x, shieldNodes[i].localPosition.y, 0));
        }

        // Edge Colliders legally require at least 2 points to exist.
        if (shieldNodes.Count > 1)
        {
            shieldCollider.enabled = true;

            // Convert our Nodes back into a Vector2 array for the physics engine
            Vector2[] physicsPoints = new Vector2[shieldNodes.Count];
            for (int i = 0; i < shieldNodes.Count; i++)
            {
                physicsPoints[i] = shieldNodes[i].localPosition;
            }
            shieldCollider.points = physicsPoints;
        }
        else
        {
            // If we only have 1 point left, physics can't run, so turn the wall off early
            shieldCollider.enabled = false;
        }
    }

    private void FadeOldShieldInk()
    {
        if (shieldNodes.Count == 0) return;

        bool nodesRemoved = false;

        // Look at the oldest node (Index 0). If it's older than our duration, delete it!
        // We use a while-loop because multiple points might expire in the exact same frame.
        while (shieldNodes.Count > 0 && Time.time - shieldNodes[0].birthTime > shieldDuration)
        {
            shieldNodes.RemoveAt(0);
            nodesRemoved = true;
        }

        // If we deleted points, we need to update the visual line and the physics wall
        if (nodesRemoved)
        {
            UpdateShieldVisuals();

            // If the entire shield faded away, completely turn off the colliders
            if (shieldNodes.Count == 0)
            {
                shieldLine.enabled = false;
                shieldCollider.enabled = false;
            }
        }
    }

    #endregion


    void Update()
    {
        // Gluing the floating pivot to the player's position
        if (playerTransform != null)
        {
            transform.position = playerTransform.position;
        }
        FadeOldShieldInk(); // Continuously check if any shield points need to fade away
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
}