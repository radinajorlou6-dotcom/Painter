using System.IO;
using UnityEngine;

/// <summary>
/// Stores the save as a single file inside Unity's per-user persistent data folder.
/// This is the real implementation of IStorage for the shipping game.
/// </summary>
public class FileStorage : IStorage
{
    private readonly string path;

    public FileStorage(string fileName)
    {
        // persistentDataPath is a safe, writable, per-user location on every platform.
        path = Path.Combine(Application.persistentDataPath, fileName);
    }

    public bool Exists() => File.Exists(path);

    public string Read() => File.ReadAllText(path);

    public void Write(string text)
    {
        // Atomic write: write to a temp file first, then swap it in. If the game
        // crashes or loses power mid-write, the original save is left untouched
        // instead of being half-written and corrupted.
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, text);

        if (File.Exists(path)) File.Delete(path);
        File.Move(tempPath, path);

        DebugUtils.Log("Game saved to: " + path);
    }
}
