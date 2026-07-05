using System.Collections.Generic;

/// <summary>
/// Orchestrates saving and loading. This is the only class that touches the serializer
/// and storage, and it receives both through its constructor (dependency injection) so
/// it can be pointed at files in the real game and at a fake in-memory store in tests.
///
/// Save  = ask every saveable for a snapshot -> pack into a SaveFile -> serialize -> write.
/// Load  = read -> deserialize -> migrate -> hand each saveable its matching snapshot.
/// </summary>
public class SaveService
{
    // Bump this when the save layout changes, and handle the upgrade in Migrate().
    public const int CurrentVersion = 1;

    private readonly ISerializer serializer;
    private readonly IStorage storage;

    public SaveService(ISerializer serializer, IStorage storage)
    {
        this.serializer = serializer;
        this.storage = storage;
    }

    public bool SaveExists() => storage.Exists();

    public void Save(IEnumerable<ISaveable> saveables)
    {
        SaveFile file = new SaveFile { version = CurrentVersion };

        foreach (ISaveable saveable in saveables)
        {
            file.entries[saveable.SaveId] = saveable.CaptureState();
        }

        storage.Write(serializer.Serialize(file));
    }

    public void Load(IEnumerable<ISaveable> saveables)
    {
        SaveFile file = ReadSaveFile();
        if (file == null) return; // no save yet, or unreadable -> systems keep their defaults

        file = Migrate(file);

        foreach (ISaveable saveable in saveables)
        {
            if (file.entries != null && file.entries.TryGetValue(saveable.SaveId, out object state))
            {
                saveable.RestoreState(state);
            }
        }
    }

    /// <summary>
    /// Reads a single system's snapshot from disk WITHOUT applying it. Handy for menus
    /// that want to preview a save (e.g. "Save found - Level 3") before the player commits.
    /// </summary>
    public bool TryLoadEntry<T>(string saveId, out T value)
    {
        value = default;

        SaveFile file = ReadSaveFile();
        if (file?.entries == null) return false;

        if (file.entries.TryGetValue(saveId, out object raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        return false;
    }

    private SaveFile ReadSaveFile()
    {
        if (!storage.Exists()) return null;

        try
        {
            return serializer.Deserialize<SaveFile>(storage.Read());
        }
        catch
        {
            // Corrupt / half-written / hand-edited file. Fall back to defaults rather than crash.
            DebugUtils.LogError("Save file could not be read. Ignoring it and using defaults.");
            return null;
        }
    }

    /// <summary>
    /// Upgrades an older save layout to the current one. Empty today; when you change the
    /// schema, add steps like: if (file.version < 2) { ...transform...; file.version = 2; }
    /// </summary>
    private SaveFile Migrate(SaveFile file)
    {
        return file;
    }
}
