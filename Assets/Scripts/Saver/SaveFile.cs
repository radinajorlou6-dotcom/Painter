using System.Collections.Generic;

/// <summary>
/// The on-disk container. Holds a format version (for future migrations) and one entry
/// per saveable system, keyed by that system's SaveId. One file can therefore store the
/// data of many systems: "progression" -> progress snapshot, "inventory" -> items, etc.
/// </summary>
[System.Serializable]
public class SaveFile
{
    public int version;
    public Dictionary<string, object> entries = new Dictionary<string, object>();
}
