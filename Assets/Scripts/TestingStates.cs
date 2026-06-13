using UnityEngine;
// 1. WE MUST INCLUDE THIS LINE TO USE THE NEW SYSTEM:
using UnityEngine.InputSystem; 

public class TestingStates : MonoBehaviour
{
    void Update()
    {
        // Safety check: If no keyboard is plugged in, don't execute
        if (Keyboard.current == null) return;

        // 2. NEW SYSTEM SYNTAX: Check if the 'P' key was pressed this frame
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            if (GameManager.Instance.CurrentState == GameManager.GameState.Playing)
            {
                Debug.Log("Tester: Requesting PAUSE state.");
                GameManager.Instance.UpdateGameState(GameManager.GameState.Paused);
            }
            else if (GameManager.Instance.CurrentState == GameManager.GameState.Paused)
            {
                Debug.Log("Tester: Requesting RESUME to Gameplay state.");
                GameManager.Instance.UpdateGameState(GameManager.GameState.Playing);
            }
        }

        // 3. NEW SYSTEM SYNTAX: Check if the 'K' key was pressed this frame
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            Debug.Log("Tester: Requesting PLAYERDIED state.");
            GameManager.Instance.UpdateGameState(GameManager.GameState.Died);
        }
    }
}