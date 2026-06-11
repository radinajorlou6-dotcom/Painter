using System.Collections; // Required for Coroutines
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float xOffset = 0f;
    [SerializeField] private float yOffset = 0f;

    [Header("Ranged Attack")]
    [SerializeField] private ObjectPooling bulletPool;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float fireRate = 0.5f;
    private float nextFireTime = 0f; // Changed to track the actual game time

    [Header("Melee Attack")]
    [SerializeField] private float knockbackDuration = 0.2f;
    [SerializeField] private Transform weaponPoint;
    [SerializeField] private PolygonCollider2D hitBox;
    [SerializeField] private float meleeDuration = 0.2f;
    [SerializeField] private float meleeCooldown = 0f; //INCASE WE WANT MELEE COOLDOWN TO BE DIFFERENT FROM MELEE ANIMATION LENGTH
    [SerializeField] private float dmgMult = 1f;
    [SerializeField] private float slashRadius = 3f;
    [SerializeField] private float maxSweepAngle = 180f;
    [SerializeField] private int arcResolution = 15; // How many points to use for the curve (higher = smoother but more expensive)
    [SerializeField] private float slashKnockback = 1f;
    [SerializeField] private LayerMask enemyLayers;

    [Header("Stab Attack")]
    [SerializeField] private float stabThreshold = 0.5f;
    [SerializeField] private float stabDuration = 1f;
    [SerializeField] private float stabCooldown = 0f; //INCASE WE WANT STAB COOLDOWN TO BE DIFFERENT FROM STAB ANIMATION LENGTH
    [SerializeField] private float stabDmgMult = 2f;
    [SerializeField] private float stabKnockback = 1f;

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
    [SerializeField] private LayerMask environment;

    // Internal State
    private List<ShieldNode> shieldNodes = new List<ShieldNode>();
    private float currentPaintUsed = 0f;
    private bool outOfPaint = false;
    private int currentHitsRemaining;
    private Coroutine shieldTimerRoutine;
    private bool canAttack = true;
    
    // Public accessors for combat preview
    public float GetSlashRadius() => slashRadius;
    public LayerMask GetEnvironmentLayerMask() => environment;

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
        Vector2 clampWorldPos = (Vector2)transform.position + offset;
        if (shieldNodes.Count == 0)
        {
            RaycastHit2D hitFirst = Physics2D.Linecast(transform.position, clampWorldPos, environment);
            if (hitFirst.collider != null) return; //We hit environment abort
            shieldNodes.Add(new ShieldNode { localPosition = offset, birthTime = Time.time });
            UpdateShieldVisuals();
            return;
        }

        // Calculate the physical length of this new line segment
        float segmentLength = Vector2.Distance(shieldNodes[shieldNodes.Count - 1].localPosition, offset);

        if (segmentLength > minPointDistance)
        {
            Vector2 lastWorldPos = (Vector2)transform.position + shieldNodes[shieldNodes.Count - 1].localPosition; //Find pos of last dot
            RaycastHit2D hit = Physics2D.Linecast(lastWorldPos, clampWorldPos, environment);
            if (hit.collider != null)
            {
                outOfPaint = true; // Lock the shield if we hit an obstacle
                return;
            }
            // THE PAINT LIMIT: Check if adding this line would exceed our total ink
            if (currentPaintUsed + segmentLength > maxPaintAmount)
            {
             // Lock the shield
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
            transform.Translate(xOffset, yOffset, 0);
        }
        FadeOldShieldInk(); // Continuously check if any shield points need to fade away
    }

    public void RangedAttack(Vector2 targetWorldPos)
    {
        // 1. FIXED COOLDOWN LOGIC: Check against the actual game clock
        if (Time.time < nextFireTime) return;

        // 2. FIXED DOUBLE-MATH: targetWorldPos is already correct, just subtract firePoint!
        Vector2 direction = (targetWorldPos - (Vector2)firePoint.position).normalized;

        GameObject bullet = bulletPool.SpawnFromPool(firePoint.position, Quaternion.identity);

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

            // CLAMP: Stop counting if we hit a half-circle
            if (accumulatedAngle >= maxSweepAngle)
            {
                accumulatedAngle = maxSweepAngle;
                break;
            }
            accumulatedAngle += Mathf.Abs(step);


            previousAngle = currentAngle;
        }

        // --- PHASE 2: SHAPE GENERATION ---
        List<Vector2> polygonPoints = new List<Vector2>();

        // Calculate the exact final angle based on our running total
        float finalAngle = startAngle + (accumulatedAngle * swingSign);

        // We need to track the physical WORLD position of the blade's edge as it swings
        float startAngleRad = startAngle * Mathf.Deg2Rad;
        Vector2 prevCurveLocal = new Vector2(Mathf.Cos(startAngleRad), Mathf.Sin(startAngleRad)) * slashRadius;
        Vector2 prevCurveWorld = (Vector2)transform.position + prevCurveLocal;
        bool first = true;

        BoxCollider2D bodyCollider = playerTransform.GetComponent<BoxCollider2D>();
        // Draw a mathematically perfect, smooth curve between the Start and Final angles
        for (int i = 0; i <= arcResolution; i++)
        {
            float t = i / (float)arcResolution;
            float angleDeg = Mathf.Lerp(startAngle, finalAngle, t);
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // Calculate exactly where this chunk of the blade is
            Vector2 curveLocalPoint = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * slashRadius;
            Vector2 curveWorldPoint = (Vector2)transform.position + curveLocalPoint;

            //Debug.DrawLine(transform.position, curveWorldPoint, Color.green, 1.0f);
            //Debug.DrawLine(prevCurveWorld, curveWorldPoint, Color.green, 1.0f);

            // Raycast from player to point on first point
            if (first)
            {
                RaycastHit2D firstHit = Physics2D.Linecast(transform.position, curveWorldPoint, environment); //shoot a linecast from player to first point
                if (firstHit.collider != null) continue;
                Vector2 firstPoint = bodyCollider.ClosestPoint(curveWorldPoint);
                RaycastHit2D closingHit = Physics2D.Linecast(firstPoint, curveWorldPoint, environment);
                if (closingHit.collider != null) continue;
                polygonPoints.Add(transform.InverseTransformPoint(firstPoint));
                first = false;
            }
            else
            {
                // Shoot a laser along the perimeter edge of the blade as it swings
                RaycastHit2D edgeHit = Physics2D.Linecast(prevCurveWorld, curveWorldPoint, environment); //Check if theres obstacle between last point and curr point
                RaycastHit2D playerhit = Physics2D.Linecast(transform.position, curveWorldPoint, environment); //Check if theres obstacle between player and curr point

                if (edgeHit.collider != null || playerhit.collider != null)
                {
                    // THE SWORD HIT A WALL!
                    // We BREAK out of the loop. This stops adding points, effectively 
                    // "chopping off" the rest of the pie slice right at the wall.
                    break;
                }
            }

            // If no wall was hit, safely add the point to our hitbox shape
            polygonPoints.Add(curveLocalPoint);

            // Update the tracker for the next frame of the swing
            prevCurveWorld = curveWorldPoint;
        }

        Vector2 lastPoint = bodyCollider.ClosestPoint(prevCurveWorld);
        polygonPoints.Add(transform.InverseTransformPoint(lastPoint));

        // Apply the perfect shape and start the attack!
        hitBox.SetPath(0, polygonPoints.ToArray());

        if (accumulatedAngle < stabThreshold)
        {
            StartCoroutine(MeleeAttackRoutine(startDirection.normalized, stabDmgMult, stabKnockback, stabDuration));
            return;
        }
        Vector2 slashDir = (swipePath[swipePath.Count - 1] - swipePath[0]).normalized;
        StartCoroutine(MeleeAttackRoutine(slashDir, dmgMult, slashKnockback, meleeDuration));
    }

    private IEnumerator MeleeAttackRoutine(Vector2 attackDir, float dmg, float knockback, float attackDur)
    {
        // Turn the visual/physics shape on
        hitBox.gameObject.SetActive(true);

        // Wait one frame so the physics engine has a chance to register the enabled collider
        // This makes overlap queries more reliable immediately after enabling a collider
        yield return null;

        // Create a filter to tell Unity exactly what layer to look for
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayers);
        filter.useTriggers = true; ; // Set to false if your enemies use physical colliders instead of triggers

        List<Collider2D> hitEnemies = new List<Collider2D>();

        // Grab everything currently touching our custom Polygon Collider using the collider instance
        // This ensures the actual polygon shape is used for the overlap test
        hitBox.Overlap(filter, hitEnemies);

        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(10f * dmg);
                if (enemyHealth != null) StartCoroutine(enemyHealth.TakeKnockback(attackDir, knockback, knockbackDuration));
            }
            Debug.Log("Hit enemy: " + enemy.name);
        }

        // Wait for a split second so the player can actually see the slash
        yield return new WaitForSeconds(attackDur);

        // Turn the hitbox back off
        hitBox.gameObject.SetActive(false);
    }
}