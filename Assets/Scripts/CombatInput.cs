using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // Required to use Lists

public class CombatInput : MonoBehaviour
{
    [SerializeField] private PlayerCombat playerCombat;

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

    void Start()
    {
        // Caching the main camera is an essential performance optimization
        mainCam = Camera.main;
    }

    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        if (context.started) // When the player presses down
        {
            isDragging = true;
            mousePath.Clear(); // Clear the old slash

            // Record the first point in World Space
            Vector2 worldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mousePath.Add(worldPos);
        }
        else if (context.canceled) // When player lets go 
        {
            isDragging = false;

            // Make sure we have at least one point to avoid errors
            if (mousePath.Count == 0) return;

            // Record the absolute final point
            Vector2 endWorldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mousePath.Add(endWorldPos);

            // Calculate distance between the very first point and the final point
            float distance = Vector2.Distance(mousePath[0], endWorldPos);

            if (distance < dragThreshold)
            {
                // The drag was too short. Treat it as a Tap/Click to shoot!
                Debug.Log("Ranged Attack! Distance: " + distance);
                playerCombat.RangedAttack(mousePath[0]);
            }
            else
            {
                // They dragged it far enough! Pass the full breadcrumb list to the muscle.
                Debug.Log("Swung Melee! Path length (breadcrumbs): " + mousePath.Count);

                // IMPORTANT: Make sure you update your playerCombat script to accept the List!
                playerCombat.ExecuteDynamicSlash(mousePath);
            }
        }
    }

    public void ShieldDefend(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            playerCombat.ToggleShield(true);
            Debug.Log("Shield Activated");
        }
        else if (context.canceled)
        {
            playerCombat.ToggleShield(false);
            Debug.Log("Shield Deactivated");
        }
    }

    void Update()
    {
        // If the player is currently holding down the attack button, record the path
        if (isDragging)
        {
            Vector2 currentWorldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 lastPoint = mousePath[mousePath.Count - 1];

            // Only drop a breadcrumb if we moved far enough from the last point
            if (Vector2.Distance(lastPoint, currentWorldPos) > minDragDistance)
            {
                mousePath.Add(currentWorldPos);
            }
            slashTimer += Time.deltaTime;
            if (slashTimer >= slashDuration)
            {
                isDragging = false;
                slashTimer = 0f;
                playerCombat.ExecuteDynamicSlash(mousePath);
                mousePath.Clear();
            }
        }
    }
}