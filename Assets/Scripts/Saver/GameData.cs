using System.Collections.Generic;

[System.Serializable]
public class GameData
{
    public int saveVersion;
    public int highestLevelReached;
    public int lastLevelPlayed;
    public List<string> unlockedColours;
    public Dictionary<string, bool> unlockedAbilities;

    public GameData()
    {
        saveVersion = 1;
        highestLevelReached = 0;
        lastLevelPlayed = 0;
        unlockedColours = new List<string>();
        unlockedAbilities = new Dictionary<string, bool>();
    }
}
