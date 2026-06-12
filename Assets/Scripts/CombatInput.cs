using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Xml; 
using System.Collections;
using JetBrains.Annotations;

public class CombatInput : MonoBehaviour
{
    
    [Header("Interaction System")]
    [Header("Interaction System")]
    [SerializeField] private Transform interactionPoint; // An empty GameObject placed in front of the player
    [SerializeField] private float interactionRadius = 0.5f; // How close you need to be to interact
    [SerializeField] private LayerMask interactableLayer; // Layer for interactable objects   

    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private Collider2D playerCollider; // Reference to the player's collider for shield point checks

    [Header("Combat Tuning")]
    [Tooltip("Minimum distance in World Units for a click to become a slash")]
    [SerializeField] private float dragThreshold = 1.5f;
    [SerializeField] private float slashDuration = 0.5f; // How long the slash effect should last (seconds)
    private float slashTimer = 0f; // Timer to track slash duration

    [Tooltip("How far the mouse must move to drop a new breadcrumb (World Units)")]
    [SerializeField] private float minDragDistance = 0.1f;

    // Mouse tracker variables
    private List<Vector2> mousePath = new List<Vector2>();
    private bool isDragging = false;
    private Camera mainCam;

    // Slash preview visualization
    private GameObject slashPreviewObject;
    private LineRenderer slashPreviewRenderer;

    private bool isShieldActive = false;

    [Header("Platform Drawing")]
    [SerializeField] private PlayerPlatform drawScript;
    [SerializeField] private GameObject drawnPlatformPrefab; // Prefab for the drawn platform (with LineRenderer and EdgeCollider2D)
    [SerializeField] private List<GameObject> drawnLines = new List<GameObject>(); //List of all the lines currently drawn
    [SerializeField] private float drawCooldown = 1f;
    [SerializeField] private int maxLines = 10;
    private int numOfLines = 0;
    public float maxDrawInk = 50f; // Max ink for drawing platforms
    private GameObject newLine; // Reference to the currently drawn line's GameObject
    private int currentLineIndex = -1; // Index of the current line being drawn in the drawnLines list
    public float currentDrawInk = 0f; // Current total ink used
    private bool isDrawActive = false;

    void Start()
    {
        mainCam = Camera.main;
        StartCoroutine(RemoveDrawLines(10f)); // Start the coroutine to remove old drawn lines every 10 seconds
    }

    public void Interact(InputAction.CallbackContext context)
    {
    // ONLY fire on the exact frame the key is physically pressed down
    if (context.performed)
    {
        // Draw a temporary mathematical circle to see if any interactable colliders are inside it
        Collider2D hit = Physics2D.OverlapCircle(interactionPoint.position, interactionRadius, interactableLayer);

        if (hit != null)
        {
            // Ask the object: "Do you have an IInteractable interface contract attached?"
            if (hit.TryGetComponent(out IInteractable interactable))
            {
                interactable.Interact(); // Pass the baton! The chest script takes over execution here.
            }
        }
    }
    }

// Visualizes the interaction circle in the Unity Editor scene view
    private void OnDrawGizmosSelected()
    {
        if (interactionPoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(interactionPoint.position, interactionRadius);
    }
 

    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        if (isDrawActive || isShieldActive) return; // Prevent attacking while drawing or shielding
        if (context.started) // When the player presses down
        {
            isDragging = true;
            slashTimer = 0f; // Reset the timer on every new click!
            mousePath.Clear();

            Vector2 worldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mousePath.Add(worldPos);

            // Create the slash preview
            CreateSlashPreview();
        }
        else if (context.canceled) // When player lets go 
        {
            isDragging = false;

            if (mousePath.Count == 0) return;

            Vector2 endWorldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mousePath.Add(endWorldPos);

            float distance = Vector2.Distance(mousePath[0], endWorldPos);

            if (distance < dragThreshold)
            {
                playerCombat.RangedAttack(mousePath[0]);
            }
            else
            {
                playerCombat.ExecuteDynamicSlash(mousePath);
            }

            // Destroy the slash preview
            DestroySlashPreview();
        }
    }

    public void ShieldDefend(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (isDrawActive || isDragging || Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed || 
                !GameManager.Instance.unlockedAbilities.ContainsKey("ShieldDraw") || !GameManager.Instance.unlockedAbilities["ShieldDraw"]) return; // Prevent shielding while drawing or slashing

            isShieldActive = true;
            playerCombat.StartNewShield();
        }
        else if (context.canceled)
        {
            isShieldActive = false;
        }
    }

    public void DrawPlatform(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (!GameManager.Instance.unlockedAbilities.ContainsKey("PlatformDraw") || !GameManager.Instance.unlockedAbilities["PlatformDraw"]) return;
            Vector2 mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            if (playerCollider.OverlapPoint(mousePos)) return; // Prevent drawing if mouse is over the player
            if (numOfLines >= maxLines) return;
            if (currentDrawInk >= maxDrawInk) return;
            newLine = Instantiate(drawnPlatformPrefab); // Create a new instance of the drawn platform prefab
            drawScript = newLine.GetComponent<PlayerPlatform>();
            isDrawActive = true;
            drawScript.StartDraw();
            numOfLines++;
        }
        else if (context.canceled)
        {
            currentLineIndex++; // Move to the next line index
            drawnLines.Add(newLine); // Add the new line to the list of drawn lines
            isDrawActive = false;
        }
    }

    private IEnumerator RemoveDrawLines(float timer)
    {
        while (true) {
            drawnLines.RemoveAll(drawnLine => drawnLine == null); // Remove any null entries from the list
            if(drawnLines.Count == 0)
            {
                StopCoroutine("returnInk");
                currentDrawInk = 0;
            }
            yield return new WaitForSeconds(timer);
        }
    }

    public IEnumerator returnInk(float inkToBeReturned)
    {
        numOfLines--;
        yield return new WaitForSeconds(drawCooldown);
        currentDrawInk -= inkToBeReturned;
    }

    void Update()
    {
        if (isDrawActive && isDragging)
        {
            // Cancel any active slash while drawing to keep preview and attack disabled
            isDragging = false;
            DestroySlashPreview();
            mousePath.Clear();
        }

        // 1. --- SLASH LOGIC ---
        if (isDragging)
        {
            Vector2 currentWorldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 lastPoint = mousePath[mousePath.Count - 1];

            if (Vector2.Distance(lastPoint, currentWorldPos) > minDragDistance)
            {
                mousePath.Add(currentWorldPos);
            }

            // Update the slash preview visualization
            UpdateSlashPreview();

            // The Auto-Slash Timeout
            slashTimer += Time.deltaTime;
            if (slashTimer >= slashDuration)
            {
                isDragging = false;
                slashTimer = 0f;

                // Safety check: Don't execute a slash if they barely moved before the timeout
                float distanceTraveled = Vector2.Distance(mousePath[0], currentWorldPos);
                if (distanceTraveled >= dragThreshold)
                {
                    playerCombat.ExecuteDynamicSlash(mousePath);
                }

                // Destroy the slash preview on timeout
                DestroySlashPreview();

                mousePath.Clear();
            }
        }

        // 2. --- SHIELD LOGIC (FIX 1: Added back in!) ---
        if (isShieldActive)
        {
            Vector2 currentWorldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            playerCombat.AddShieldPoint(currentWorldPos);
        }

        if (isDrawActive)
        {
            Vector2 currentWorldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            if (playerCollider.OverlapPoint(currentWorldPos)) return; // Prevent drawing if mouse is over the player
            drawScript.UpdateDraw(currentWorldPos);
        }
    }

    #region SlashPreview
    private void CreateSlashPreview()
    {
        // Create a new GameObject to hold the LineRenderer
        slashPreviewObject = new GameObject("SlashPreview");
        
        // Add LineRenderer component
        slashPreviewRenderer = slashPreviewObject.AddComponent<LineRenderer>();
        
        // Configure the LineRenderer
        slashPreviewRenderer.startWidth = 0.1f;
        slashPreviewRenderer.endWidth = 0.1f;
        
        // Set the color (light cyan for preview)
        slashPreviewRenderer.startColor = new Color(0, 1, 1, 0.6f);
        slashPreviewRenderer.endColor = new Color(0, 1, 1, 0.6f);
        
        // Set the material
        slashPreviewRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void UpdateSlashPreview()
    {
        if (slashPreviewRenderer == null || mousePath.Count < 2) return;

        // Generate the hitbox polygon points
        List<Vector2> polygonPoints = GenerateSlashPolygon(mousePath);
        
        // Draw the polygon using the LineRenderer
        if (polygonPoints.Count > 0)
        {
            // Create a closed loop by adding the first point at the end
            slashPreviewRenderer.positionCount = polygonPoints.Count + 1;
            
            for (int i = 0; i < polygonPoints.Count; i++)
            {
                slashPreviewRenderer.SetPosition(i, new Vector3(polygonPoints[i].x, polygonPoints[i].y, 0));
            }
            // Close the loop
            slashPreviewRenderer.SetPosition(polygonPoints.Count, new Vector3(polygonPoints[0].x, polygonPoints[0].y, 0));
        }
    }

    private List<Vector2> GenerateSlashPolygon(List<Vector2> swipePath)
    {
        List<Vector2> polygonPoints = new List<Vector2>();
        
        if (swipePath == null || swipePath.Count < 2) return polygonPoints;

        // Get player transform from PlayerCombat
        Transform playerTransform = playerCombat.GetComponent<Transform>();
        if (playerTransform == null) return polygonPoints;

        // Get the actual slash radius and environment layer from PlayerCombat
        float slashRadius = playerCombat.GetSlashRadius();
        LayerMask environment = playerCombat.GetEnvironmentLayerMask();

        // --- PHASE 1: ANGLE CALCULATION ---
        Vector2 startDirection = swipePath[0] - (Vector2)playerTransform.position;
        float startAngle = Mathf.Atan2(startDirection.y, startDirection.x) * Mathf.Rad2Deg;

        float previousAngle = startAngle;
        float accumulatedAngle = 0f;
        float swingSign = 0f;

        foreach (Vector2 point in swipePath)
        {
            Vector2 direction = point - (Vector2)playerTransform.position;
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
                previousAngle = currentAngle;
                continue;
            }

            // CLAMP: Stop counting if we hit a half-circle (180 degrees)
            if (accumulatedAngle >= 180f)
            {
                accumulatedAngle = 180f;
                break;
            }
            accumulatedAngle += Mathf.Abs(step);
            previousAngle = currentAngle;
        }

        if (accumulatedAngle < 0.01f) return polygonPoints; // No meaningful swing yet

        // --- PHASE 2: SHAPE GENERATION ---
        float finalAngle = startAngle + (accumulatedAngle * swingSign);
        int arcResolution = 15;

        // Add the player center as the first point
        polygonPoints.Add((Vector2)playerTransform.position);

        // Get the body collider for closest point calculations
        BoxCollider2D bodyCollider = playerTransform.GetComponent<BoxCollider2D>();

        // Draw a mathematically perfect curve between the Start and Final angles
        Vector2 prevCurveWorld = (Vector2)playerTransform.position;
        bool first = true;

        for (int i = 0; i <= arcResolution; i++)
        {
            float t = i / (float)arcResolution;
            float angleDeg = Mathf.Lerp(startAngle, finalAngle, t);
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // Calculate exactly where this chunk of the blade is
            Vector2 curveLocalPoint = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * slashRadius;
            Vector2 curveWorldPoint = (Vector2)playerTransform.position + curveLocalPoint;

            // --- WALL COLLISION DETECTION (Same as in PlayerCombat) ---
            if (first)
            {
                RaycastHit2D firstHit = Physics2D.Linecast(playerTransform.position, curveWorldPoint, environment);
                if (firstHit.collider != null)
                {
                    first = false;
                    break; // Wall hit on first point, stop here
                }
                
                if (bodyCollider != null)
                {
                    Vector2 firstPoint = bodyCollider.ClosestPoint(curveWorldPoint);
                    RaycastHit2D closingHit = Physics2D.Linecast(firstPoint, curveWorldPoint, environment);
                    if (closingHit.collider != null)
                    {
                        first = false;
                        break; // Wall hit between player and edge, stop here
                    }
                }
                
                first = false;
            }
            else
            {
                // Check if the swing edge hits a wall
                RaycastHit2D edgeHit = Physics2D.Linecast(prevCurveWorld, curveWorldPoint, environment);
                // Also check if the line from player to this point hits a wall
                RaycastHit2D playerHit = Physics2D.Linecast(playerTransform.position, curveWorldPoint, environment);

                if (edgeHit.collider != null || playerHit.collider != null)
                {
                    // HIT A WALL! Stop expanding the slash
                    break;
                }
            }

            // If no wall was hit, add the point to our polygon
            polygonPoints.Add(curveWorldPoint);
            prevCurveWorld = curveWorldPoint;
        }

        return polygonPoints;
    }

    private void DestroySlashPreview()
    {
        if (slashPreviewObject != null)
        {
            Destroy(slashPreviewObject);
        }
        slashPreviewRenderer = null;
    }
    #endregion
}