using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

/// <summary>
/// Central game controller. Persists across scenes and owns:
///   - The current game state (Playing / Paused / Died) and its side effects
///   - All persistent progress data (level reached, unlocked colours/abilities)
///   - Save / load orchestration and scene transitions
/// UI scripts never touch SaveSystem or SceneManager directly; they call into
/// this class so all progression logic lives in one place.
/// </summary>
public class GameManager : MonoBehaviour
{
    // --- Input action maps (resolved per scene) ---
    private InputActionMap movementMap;
    private InputActionMap combatMap;
    private InputActionMap uiMap;
    private PlayerInput playerInput;

    public enum GameState
    {
        Playing,
        Paused,
        Died,
    }

    [Header("Scene Settings")]
    [Tooltip("Name of the main menu scene. Must be added to File > Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Name of the first gameplay level. Must be added to File > Build Settings.")]
    [SerializeField] private string firstLevelSceneName = "Tutorial";

    // --- Singleton + events ---
    public static GameManager Instance { get; private set; }
    public GameState CurrentState { get; private set; }
    public static event Action<GameState> OnStateChanged;
    public static event Action<string> OnColourUnlocked;

    // --- Persistent progress ---
    public int maxLevelReached { get; private set; } = 1;

    public Dictionary<string, bool> unlockedAbilities { get; private set; } = new Dictionary<string, bool>()
    {
        {"Slingshot", true},
        {"PlatformDraw", true},
        {"ShieldDraw", true}
    };

    public List<string> unlockedColours { get; private set; } = new List<string>();

    private Dictionary<string, bool> bucketStates = new Dictionary<string, bool>();

    #region Unity Lifecycle
    private void Awake()
    {
        // Enforce a single persistent instance
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveInput();
    }

    private void OnEnable()
    {
        // Re-resolve input references every time a new scene finishes loading,
        // because the PlayerInput component lives in the gameplay scene, not here.
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveInput();
        RefreshInputMapStates();
    }
    #endregion

    #region Progress Tracking
    public bool IsBucketEmpty(string colour)
    {
        return bucketStates.ContainsKey(colour) && bucketStates[colour];
    }

    public void SaveBucketState(string colour, bool isEmpty)
    {
        bucketStates[colour] = isEmpty;
        OnColourUnlocked?.Invoke(colour);
    }

    public void UnlockAbility(string abilityName)
    {
        if (unlockedAbilities.ContainsKey(abilityName) && !unlockedAbilities[abilityName])
        {
            unlockedAbilities[abilityName] = true;
        }
    }

    public void UnlockColour(string colourName)
    {
        if (!unlockedColours.Contains(colourName))
        {
            unlockedColours.Add(colourName);
            OnColourUnlocked?.Invoke(colourName);
        }
    }

    public void UpdateMaxLevelReached(int levelIndex)
    {
        maxLevelReached = Mathf.Max(maxLevelReached, levelIndex);
    }
    #endregion

    #region Save / Load
    /// <summary>Snapshots the current progress into a serializable GameData object.</summary>
    public GameData CaptureState()
    {
        return new GameData
        {
            highestLevelReached = maxLevelReached,
            unlockedColours = new List<string>(unlockedColours),
            unlockedAbilities = new Dictionary<string, bool>(unlockedAbilities)
        };
    }

    /// <summary>Overwrites current progress with the values from a loaded GameData object.</summary>
    public void ApplyState(GameData data)
    {
        if (data == null) return;

        maxLevelReached = Mathf.Max(1, data.highestLevelReached);
        unlockedColours = data.unlockedColours ?? new List<string>();

        if (data.unlockedAbilities != null)
        {
            unlockedAbilities = data.unlockedAbilities;
        }

        // Notify any listening environment objects that colours are unlocked
        foreach (string colour in unlockedColours)
        {
            OnColourUnlocked?.Invoke(colour);
        }
    }

    /// <summary>Writes the current progress to disk.</summary>
    public void SaveGame()
    {
        SaveSystem.SaveGame(CaptureState());
    }

    public bool HasSaveFile()
    {
        return SaveSystem.SaveExists();
    }
    #endregion

    #region Flow Control (New Game / Continue / Menu / Quit)
    /// <summary>Resets all progress to defaults and loads the first level.</summary>
    public void NewGame()
    {
        maxLevelReached = 1;
        unlockedColours = new List<string>();
        unlockedAbilities = new Dictionary<string, bool>()
        {
            {"Slingshot", true},
            {"PlatformDraw", true},
            {"ShieldDraw", true}
        };
        bucketStates = new Dictionary<string, bool>();

        CurrentState = GameState.Playing;
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstLevelSceneName);
    }

    /// <summary>Loads saved progress from disk and jumps to the saved level.</summary>
    public void ContinueGame()
    {
        GameData data = SaveSystem.LoadGame();
        ApplyState(data);

        CurrentState = GameState.Playing;
        Time.timeScale = 1f;

        // highestLevelReached is stored as a build index. Clamp it so we never
        // accidentally load the menu (index 0) or an out-of-range scene.
        int sceneIndex = Mathf.Clamp(data.highestLevelReached, 1, SceneManager.sceneCountInBuildSettings - 1);
        SceneManager.LoadScene(sceneIndex);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        CurrentState = GameState.Playing; // reset so the next gameplay scene starts clean
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        DebugUtils.Log("Quitting game.");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    #endregion

    #region State Manager
    public void UpdateGameState(GameState newState)
    {
        CurrentState = newState;

        switch (newState)
        {
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                break;
            case GameState.Died:
                Time.timeScale = 0f;
                break;
            default:
                DebugUtils.Log("Wrong GameState name used");
                break;
        }

        RefreshInputMapStates();
        OnStateChanged?.Invoke(newState);
    }

    /// <summary>Finds the PlayerInput in the current scene and caches its action maps.</summary>
    private void ResolveInput()
    {
        playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput == null)
        {
            // Expected in menu scenes that have no player
            movementMap = combatMap = uiMap = null;
            return;
        }

        movementMap = playerInput.actions.FindActionMap("Player");
        combatMap = playerInput.actions.FindActionMap("Combat");
        uiMap = playerInput.actions.FindActionMap("UI");
    }

    /// <summary>Enables/disables input maps to match the current state.</summary>
    private void RefreshInputMapStates()
    {
        if (playerInput == null) return;

        bool isPlaying = CurrentState == GameState.Playing;

        if (isPlaying)
        {
            movementMap?.Enable();
            combatMap?.Enable();
//            uiMap?.Disable();
        }
        else
        {
            movementMap?.Disable();
            combatMap?.Disable();
            uiMap?.Enable();
        }
    }
    #endregion
}
