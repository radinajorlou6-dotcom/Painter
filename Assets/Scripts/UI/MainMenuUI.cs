using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the title screen. New Game starts fresh; Continue loads the save
/// file and jumps to the saved level. The Continue button is automatically
/// disabled when no save file exists so the player can't load nothing.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("Disabled automatically when there is no save file to load.")]
    [SerializeField] private Button continueButton;

    [Header("Feedback (optional)")]
    [Tooltip("Text that shows whether a save was found, e.g. 'Save found - Level 3'.")]
    [SerializeField] private TMP_Text saveStatusText;

    private void Start()
    {
        RefreshSaveStatus();
    }

    // --- Button hooks (wire these in the Button's OnClick in the Inspector) ---

    public void OnNewGameClicked()
    {
        GameManager.Instance.NewGame();
    }

    public void OnContinueClicked()
    {
        GameManager.Instance.ContinueGame();
    }

    public void OnQuitClicked()
    {
        GameManager.Instance.QuitGame();
    }

    // --- Helpers ---

    private void RefreshSaveStatus()
    {
        bool saveExists = GameManager.Instance != null && GameManager.Instance.HasSaveFile();

        if (continueButton != null)
        {
            continueButton.interactable = saveExists;
        }

        if (saveStatusText != null)
        {
            if (GameManager.Instance.TryGetSavedProgress(out GameData data))
            {
                saveStatusText.text = $"Save found - Level {data.highestLevelReached}";
            }
            else
            {
                saveStatusText.text = "Save found";
            }
        }
    }
}
