using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Drives the in-game pause and game-over overlays.
/// This script is intentionally "dumb": it reacts to GameManager state changes
/// to show/hide panels, and its buttons forward requests back to the GameManager.
/// It never touches SaveSystem or SceneManager directly.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Panels (assign the root GameObjects)")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Feedback (optional)")]
    [Tooltip("Text shown briefly after saving, e.g. 'Game Saved!'")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private float feedbackDuration = 1.5f;

    private Coroutine feedbackRoutine;

    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleStateChanged;
    }

    private void Start()
    {
        // Make sure everything starts hidden regardless of how it was left in the editor
        SetPanel(pausePanel, false);
        SetPanel(gameOverPanel, false);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        SetPanel(pausePanel, newState == GameManager.GameState.Paused);
        SetPanel(gameOverPanel, newState == GameManager.GameState.Died);
    }

    // --- Button hooks (wire these in the Button's OnClick in the Inspector) ---

    public void OnResumeClicked()
    {
        GameManager.Instance.UpdateGameState(GameManager.GameState.Playing);
    }

    public void OnSaveClicked()
    {
        GameManager.Instance.SaveGame();
        ShowFeedback("Game Saved!");
    }

    public void OnLoadClicked()
    {
        GameManager.Instance.ContinueGame();
    }

    public void OnMainMenuClicked()
    {
        GameManager.Instance.ReturnToMainMenu();
    }

    public void OnQuitClicked()
    {
        GameManager.Instance.QuitGame();
    }

    // --- Helpers ---

    private void SetPanel(GameObject panel, bool visible)
    {
        if (panel != null) panel.SetActive(visible);
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText == null) return;

        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);

        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(HideFeedbackAfterDelay());
    }

    private IEnumerator HideFeedbackAfterDelay()
    {
        // Use realtime because the game is paused (Time.timeScale == 0)
        yield return new WaitForSecondsRealtime(feedbackDuration);
        feedbackText.gameObject.SetActive(false);
    }
}
