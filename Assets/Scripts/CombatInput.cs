//This script is intended to figure out what the player is trying to do in combat and then call the appropriate functions in playerCombat.cs
using UnityEngine;
using UnityEngine.InputSystem;


public class CombatInput : MonoBehaviour
{
    [SerializeField] private PlayerCombat playerCombat; //Reference to the PlayerCombat script, assign in inspector

    //Mouse tracker variables
    private Vector2 startPoint; //Where the player starts dragging the mouse
    private Vector2 endPoint; //Where the player releases the mouse
    [SerializeField] private float dragThreshold = 0.5f; //Minimum distance for a drag to be a melee

    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        if (context.started) //When the player presses down
        {
            startPoint = Mouse.current.position.ReadValue(); //Record initial mouse position
        }
        if (context.canceled) //When player lets go 
        {
            endPoint = Mouse.current.position.ReadValue(); ; //Record final mouse position
            float distance = Vector2.Distance(startPoint, endPoint); //Calculate distance between start and end points
            if (distance < dragThreshold)
            {
                // Call ranged attack function in playerCombat.cs
                Debug.Log("Ranged Attack\n distance: " + distance);
                playerCombat.RangedAttack(startPoint);
            }
            else
            {
                // 1. Calculate the raw vector (Destination minus Origin)
                Vector2 rawDirection = endPoint - startPoint;

                // 2. Normalize it (Shrink length to 1, keep the exact direction)
                Vector2 swipeDirection = rawDirection.normalized;

                // Test it! Your console will output a clean vector like (0.5, -0.5)
                Debug.Log("Swung Melee! Direction is: " + swipeDirection + "\n distance: " + distance);

                playerCombat.PerformMelee(swipeDirection);
            }
        }
    }

    public void Shielddefend(InputAction.CallbackContext context)
    {
        if (context.started) {
            playerCombat.ToggleShield(true);
            Debug.Log("Shield Activated");
        }
        else if (context.canceled) {
            playerCombat.ToggleShield(false);
            Debug.Log("Shield Deactivated");
        }
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }
}
