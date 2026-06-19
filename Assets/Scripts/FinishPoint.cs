using UnityEngine;
using UnityEngine.SceneManagement;

public class FinishPoint : MonoBehaviour
{
    [SerializeField] private string nextSceneName;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player") || collision.gameObject.CompareTag("Player"))
        {
            //DO STUFF GIVE POWER GIVE COLOUR ETC
            GameManager.Instance.UpdateMaxLevelReached(SceneManager.GetActiveScene().buildIndex + 1); // Update max level reached
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
