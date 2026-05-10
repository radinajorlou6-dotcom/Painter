using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Xml; 
using System.Collections;
using JetBrains.Annotations;

public class CombatInput : MonoBehaviour
{
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

    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        if (context.started) // When the player presses down
        {
            isDragging = true;
            slashTimer = 0f; // Reset the timer on every new click!
            mousePath.Clear();

            Vector2 worldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mousePath.Add(worldPos);
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
                Debug.Log("Ranged Attack! Distance: " + distance);
                playerCombat.RangedAttack(mousePath[0]);
            }
            else
            {
                Debug.Log("Swung Melee! Path length: " + mousePath.Count);
                playerCombat.ExecuteDynamicSlash(mousePath);
            }
        }
    }

    public void ShieldDefend(InputAction.CallbackContext context)
    {
        if (context.started)
        {
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
        // 1. --- SLASH LOGIC ---
        if (isDragging)
        {
            Vector2 currentWorldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 lastPoint = mousePath[mousePath.Count - 1];

            if (Vector2.Distance(lastPoint, currentWorldPos) > minDragDistance)
            {
                mousePath.Add(currentWorldPos);
            }

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
}