using UnityEngine;
using System.Collections.Generic;
using System;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }
    public static event Action<String> OnColourUnlocked;
    public int maxLevelReached { get; private set; } = 1; // Track the highest level reached by the player

    //Player abilitys
    public bool hasSlingshot { get; private set; } = false;
    public bool hasPlatformDraw { get; private set; } = false;
    public bool hasShieldDraw { get; private set; } = false;

    //Colours unlocked (not sure which ones will use yet just putting eveything here for now)
    public bool hasRed { get; private set; } = false;
    public bool hasBlue { get; private set; } = false;
    public bool hasYellow { get; private set; } = false;
    public bool hasGreen { get; private set; } = false;
    public bool hasPurple { get; private set; } = false;

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
        switch (abilityName)
        {
            case "Slingshot":
                hasSlingshot = true;
                break;
            case "PlatformDraw":
                hasPlatformDraw = true;
                break;
            case "ShieldDraw":
                hasShieldDraw = true;
                break;
            default:
                Debug.LogWarning("Unknown ability: " + abilityName);
                break;
        }
    }

    public void UnlockColour(string colourName)
    {
        switch (colourName)
        {
            case "red":
                hasRed = true;
                break;
            case "blue":
                hasBlue = true;
                break;
            case "yellow":
                hasYellow = true;
                break;
            case "green":
                hasGreen = true;
                break;
            case "purple":
                hasPurple = true;
                break;
            default:
                Debug.LogWarning("Unknown colour: " + colourName);
                break;
        }
    }

    public void UpdateMaxLevelReached(int levelIndex)
    {
        maxLevelReached = Mathf.Max(maxLevelReached, levelIndex);
    }
}
