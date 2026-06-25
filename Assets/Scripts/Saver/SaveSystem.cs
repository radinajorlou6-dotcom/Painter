using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class SaveSystem
{
    private static string savePath = Path.Combine(Application.persistentDataPath, "saveFile.json");
    
    public static void SaveGame(GameData data)
    {
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(savePath, json);
        DebugUtils.Log("Game saved to: " + savePath);
    }

    public static GameData LoadGame()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            GameData data = JsonConvert.DeserializeObject<GameData>(json);
            DebugUtils.Log("Game loaded from: " + savePath);
            return data;
        }
        DebugUtils.Log("No save file found. Creating new game data.");
        return new GameData();
    }

    public static bool SaveExists()
    {
        return File.Exists(savePath);
    }
}
