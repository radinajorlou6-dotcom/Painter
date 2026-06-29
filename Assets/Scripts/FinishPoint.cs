using UnityEngine;
using UnityEngine.SceneManagement;

public class FinishPoint : MonoBehaviour
{
    [SerializeField] private string nextSceneName;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            //DO STUFF GIVE POWER GIVE COLOUR ETC
            // Level progress is recorded by GameManager.HandleSceneLoaded once the next scene
            // actually loads, where the build index is reliable. Computing it here would use
            // GetSceneByName on a scene that isn't loaded yet, which returns build index -1.
            LoadNextLevel();
        }
    }

    private void LoadNextLevel()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            DebugUtils.LogError("Next scene name is not set on FinishPoint.");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }
}
