using System.Collections.Generic;

/// <summary>
/// The player's unlockable powers. Using an enum instead of string keys means a typo is a
/// compile error instead of a silent bug, and the Inspector shows a dropdown instead of a text box.
/// </summary>
public enum AbilityType
{
    Slingshot,
    PlatformDraw,
    ShieldDraw
}

/// <summary>
/// The paint colours the world can regain as the player progresses. Add new entries here as
/// you add colours to the game. Keeps colour references type-safe across buckets, environment, and saves.
/// </summary>
public enum PaintColour
{
    Blue,
    Red,
    Green,
    Yellow
}

[System.Serializable]
public class GameData
{
    public int saveVersion;
    public int highestLevelReached;
    public int lastLevelPlayed;
    public List<PaintColour> unlockedColours;
    public Dictionary<AbilityType, bool> unlockedAbilities;

    public GameData()
    {
        saveVersion = SaveService.CurrentVersion;
        highestLevelReached = 0;
        lastLevelPlayed = 0;
        unlockedColours = new List<PaintColour>();
        unlockedAbilities = new Dictionary<AbilityType, bool>();
    }
}
