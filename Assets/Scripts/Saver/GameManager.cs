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
public class GameManager : MonoBehaviour, ISaveable
{
    // --- Save system ---
    private const string ProgressionSaveId = "progression";
    private SaveService saveService;
    private readonly List<ISaveable> saveables = new List<ISaveable>();

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

    [Header("Starting Unlocks")]
    [Tooltip("Abilities the player begins a new game with. Anything not listed stays locked until a paint bucket grants it.")]
    [SerializeField]
    private List<AbilityType> startingAbilities = new List<AbilityType>
    {
        AbilityType.Slingshot,
        AbilityType.PlatformDraw,
        AbilityType.ShieldDraw
    };

    [Tooltip("Colours the world begins a new game with. These count as already restored, so their tilemaps are coloured in and passable from the start.")]
    [SerializeField] private List<PaintColour> startingColours = new List<PaintColour>();

    // --- Singleton + events ---
    public static GameManager Instance { get; private set; }
    public GameState CurrentState { get; private set; }
    public static event Action<GameState> OnStateChanged;
    public static event Action<PaintColour> OnColourUnlocked;

    // --- Persistent progress ---
    public int maxLevelReached { get; private set; } = 1;
    public int lastLevelPlayed { get; private set; } = 1;

    // --- Checkpoint (in-level respawn point; reset each time a new level loads) ---
    public Vector3 LastCheckpoint { get; private set; }
    public bool HasCheckpoint { get; private set; }

    public Dictionary<AbilityType, bool> unlockedAbilities { get; private set; } = new Dictionary<AbilityType, bool>();

    public List<PaintColour> unlockedColours { get; private set; } = new List<PaintColour>();

    private Dictionary<PaintColour, bool> bucketStates = new Dictionary<PaintColour, bool>();

    /// <summary>
    /// Wipes live progress back to the starting set configured in the Inspector. Used at boot and
    /// when a new game begins, so those two lists are the single source of truth for what the
    /// player owns before any bucket is emptied.
    /// </summary>
    private void ApplyStartingUnlocks()
    {
        unlockedAbilities = new Dictionary<AbilityType, bool>();
        unlockedColours = new List<PaintColour>();
        bucketStates = new Dictionary<PaintColour, bool>();

        GrantStartingUnlocks();
    }

    /// <summary>
    /// Adds the Inspector's starting unlocks on top of whatever is already unlocked, never removing
    /// anything. Safe to call after loading a save, so an ability or colour you add to the starting
    /// lists later still reaches players who already have a save file.
    /// </summary>
    private void GrantStartingUnlocks()
    {
        foreach (AbilityType ability in startingAbilities)
        {
            unlockedAbilities[ability] = true;
        }

        foreach (PaintColour colour in startingColours)
        {
            if (!unlockedColours.Contains(colour)) unlockedColours.Add(colour);
            bucketStates[colour] = true; // ColourControl/PaintBucketScript read this to see the colour is restored
        }
    }

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

        // Seed progress from the Inspector lists. This can't be a field initializer, because
        // Unity fills [SerializeField] fields after those run - the lists would still be empty.
        ApplyStartingUnlocks();

        // Build the save system: JSON serializer + a file on disk. Injecting these here
        // (rather than hardcoding them inside SaveService) is what keeps SaveService testable.
        saveService = new SaveService(new JsonSaveSerializer(), new FileStorage("saveFile.json"));
        RegisterSaveable(this); // progression saves itself; other systems can register too.

        ResolveInput();
    }

    /// <summary>Systems call this to take part in saving/loading.</summary>
    public void RegisterSaveable(ISaveable saveable)
    {
        if (!saveables.Contains(saveable)) saveables.Add(saveable);
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

        // Record the level the player is in using the actually-loaded scene's build index.
        // This is reliable because the scene exists at this point; computing the index before
        // load (e.g. GetSceneByName on a scene that isn't loaded yet) returns -1.
        // Skip the main menu so it's never stored as the last level played.
        if (scene.buildIndex > 0 && scene.name != mainMenuSceneName)
        {
            UpdateMaxLevelReached(scene.buildIndex);
            HasCheckpoint = false; // fresh level: respawn at the player's start until a checkpoint is hit
        }
    }

    /// <summary>Records the active respawn point. Called by Checkpoint triggers.</summary>
    public void SetCheckpoint(Vector3 position)
    {
        LastCheckpoint = position;
        HasCheckpoint = true;
    }
    #endregion

    #region Progress Tracking
    /// <summary>True if the given ability is unlocked. Safe even if the key is missing.</summary>
    public bool IsAbilityUnlocked(AbilityType ability)
    {
        return unlockedAbilities.TryGetValue(ability, out bool unlocked) && unlocked;
    }

    public bool IsColourUnlocked(PaintColour colour)
    {
        return unlockedColours.Contains(colour);
    }

    public bool IsBucketEmpty(PaintColour colour)
    {
        return bucketStates.TryGetValue(colour, out bool empty) && empty;
    }

    public void SaveBucketState(PaintColour colour, bool isEmpty)
    {
        bucketStates[colour] = isEmpty;
        if (!isEmpty) return;

        // Emptying a bucket IS the colour being restored, so keep the unlocked list in step with
        // the bucket state. Both are saved, so neither can quietly disagree after a load.
        if (!unlockedColours.Contains(colour)) unlockedColours.Add(colour);
        OnColourUnlocked?.Invoke(colour);
    }

    public void UnlockAbility(AbilityType ability)
    {
        unlockedAbilities[ability] = true;
    }

    public void UnlockColour(PaintColour colour)
    {
        if (!unlockedColours.Contains(colour))
        {
            unlockedColours.Add(colour);
            OnColourUnlocked?.Invoke(colour);
        }
    }

    public void UpdateMaxLevelReached(int levelIndex)
    {
        lastLevelPlayed = levelIndex;
        maxLevelReached = Mathf.Max(maxLevelReached, levelIndex);
    }
    #endregion

    #region Save / Load (ISaveable)
    /// <summary>Unique key this system's data is filed under in the save.</summary>
    public string SaveId => ProgressionSaveId;

    /// <summary>Snapshots the current progress into an independent, serializable object.</summary>
    public object CaptureState()
    {
        // Copy the collections so the snapshot can't be changed by later gameplay. This is
        // what makes the design safe to move to background/autosave later on.
        return new GameData
        {
            saveVersion = SaveService.CurrentVersion,
            highestLevelReached = maxLevelReached,
            lastLevelPlayed = lastLevelPlayed,
            unlockedColours = new List<PaintColour>(unlockedColours),
            unlockedAbilities = new Dictionary<AbilityType, bool>(unlockedAbilities),
            emptiedBuckets = new Dictionary<PaintColour, bool>(bucketStates)
        };
    }

    /// <summary>Overwrites current progress with the values from a loaded snapshot.</summary>
    public void RestoreState(object state)
    {
        GameData data = state as GameData;
        if (data == null) return;

        maxLevelReached = Mathf.Max(1, data.highestLevelReached);
        lastLevelPlayed = Mathf.Max(1, data.lastLevelPlayed);
        unlockedColours = data.unlockedColours ?? new List<PaintColour>();

        if (data.unlockedAbilities != null)
        {
            unlockedAbilities = new Dictionary<AbilityType, bool>(data.unlockedAbilities);
        }

        // Older saves predate this field, so fall back to an empty set rather than crashing.
        bucketStates = data.emptiedBuckets != null
            ? new Dictionary<PaintColour, bool>(data.emptiedBuckets)
            : new Dictionary<PaintColour, bool>();

        // Applied last so the Inspector's starting unlocks survive an older save file.
        GrantStartingUnlocks();

        // Notify any listening environment objects that colours are unlocked
        foreach (PaintColour colour in unlockedColours)
        {
            OnColourUnlocked?.Invoke(colour);
        }
    }

    /// <summary>Writes the current progress (and every registered system) to disk.</summary>
    public void SaveGame()
    {
        saveService.Save(saveables);
    }

    public bool HasSaveFile()
    {
        return saveService.SaveExists();
    }

    /// <summary>Reads saved progress for display without applying it (used by the menu).</summary>
    public bool TryGetSavedProgress(out GameData data)
    {
        return saveService.TryLoadEntry(ProgressionSaveId, out data);
    }
    #endregion

    #region Flow Control (New Game / Continue / Menu / Quit)
    /// <summary>Resets all progress to defaults and loads the first level.</summary>
    public void NewGame()
    {
        maxLevelReached = 1;
        ApplyStartingUnlocks();

        CurrentState = GameState.Playing;
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstLevelSceneName);
    }

    /// <summary>Loads saved progress from disk and jumps to the saved level.</summary>
    public void ContinueGame()
    {
        // Restores progression (and any other registered system) into our live fields.
        // If there's no save, this is a no-op and lastLevelPlayed keeps its default of 1.
        saveService.Load(saveables);

        CurrentState = GameState.Playing;
        Time.timeScale = 1f;

        // lastLevelPlayed is stored as a build index. Clamp it so we never
        // accidentally load the menu (index 0) or an out-of-range scene.
        int sceneIndex = Mathf.Clamp(lastLevelPlayed, 1, SceneManager.sceneCountInBuildSettings - 1);
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
            uiMap?.Disable();
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
