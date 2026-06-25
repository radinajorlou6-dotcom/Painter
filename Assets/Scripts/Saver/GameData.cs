using System.Collections.Generic;

[System.Serializable]
public class GameData
{
    public int highestLevelReached;
    public List<string> unlockedColours;
    public Dictionary<string, bool> unlockedAbilities;

    public GameData()
    {
        highestLevelReached = 0;
        unlockedColours = new List<string>();
        unlockedAbilities = new Dictionary<string, bool>();
    }
}
