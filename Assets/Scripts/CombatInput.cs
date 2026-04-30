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

    private bool isShieldActive = false;

    void Start()
    {
        mainCam = Camera.main;
    }

    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        if (context.started) // When the player presses down
        {
            isDragging = true;
            slashTimer = 0f; // FIX 2: Reset the timer on every new click!
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
            // Note: Since we switched to fading ink segments in the last step, 
            // you might not actually need DeployShield() anymore if your PlayerCombat Update handles the fading!
            // But leaving it here is safe if you still use it.
        }
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
    }
}