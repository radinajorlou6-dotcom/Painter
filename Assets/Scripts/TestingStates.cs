using UnityEngine;
using UnityEngine.InputSystem; 

public class TestingStates : MonoBehaviour
{
    public void Pause(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (GameManager.Instance.CurrentState == GameManager.GameState.Playing){
            DebugUtils.Log("PAUSE");
            GameManager.Instance.UpdateGameState(GameManager.GameState.Paused);
        }
        else if (GameManager.Instance.CurrentState == GameManager.GameState.Paused)
        {
            DebugUtils.Log("PLAYING");
            GameManager.Instance.UpdateGameState(GameManager.GameState.Playing);
        }
    }

    public void InstaKill(InputAction.CallbackContext context)
    {
        DebugUtils.Log("DYING");
        GameManager.Instance.UpdateGameState(GameManager.GameState.Died);
    }
}