using UnityEngine;
using System.IO;

public class SaveSystem
{
    private static string savePath = Path.Combine(Application.persistentDataPath, "saveFile.json");
    public static void SaveGame(GameData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(savePath, json);
    }

    public static GameData LoadGame()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            GameData data = JsonUtility.FromJson<GameData>(json);
            return data;
        }
        return new GameData(); // Return default data if no save file exists
    }
}
