using UnityEngine;
using System.Collections.Generic;
using System;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }
    public static event Action<String> OnColourUnlocked;
    public int maxLevelReached { get; private set; } = 1; // Track the highest level reached by the player

    //Player abilitys
    public Dictionary<string, bool> unlockedAbilities { get; private set; } = new Dictionary<string, bool>()
    {
        {"Slingshot", true},
        {"PlatformDraw", true},
        {"ShieldDraw", true}
    };

    //Colours unlocked (not sure which ones will use yet just putting eveything here for now)
    public List<string> unlockedColours { get; private set; } = new List<string>() {};

    //Paint bucket states
    private Dictionary<string, bool> bucketStates = new Dictionary<string, bool>();

    private void Awake()
    {
        // Ensure that there's only one instance of GameManager
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: Persist across scenes
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Instance = this;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public bool IsBucketEmpty(string colour)
    {
        return bucketStates.ContainsKey(colour) && bucketStates[colour];
    }

    public void SaveBucketState(string colour, bool isEmpty)
    {
        bucketStates[colour] = isEmpty;
        OnColourUnlocked?.Invoke(colour); // Notify listeners that a colour has been unlocked
        
    }

    public void UnlockAbility(string abilityName)
    {
        if (unlockedAbilities.ContainsKey(abilityName) && !unlockedAbilities[abilityName])
        {
            unlockedAbilities[abilityName] = true;
            // You can also add an event here to notify listeners that an ability has been unlocked
        }
    }

    public void UnlockColour(string colourName)
    {
        if (!unlockedColours.Contains(colourName))
        {
            unlockedColours.Add(colourName);
            OnColourUnlocked?.Invoke(colourName); // Notify listeners that a colour has been unlocked
        }
    }

    public void UpdateMaxLevelReached(int levelIndex)
    {
        maxLevelReached = Mathf.Max(maxLevelReached, levelIndex);
    }
}
