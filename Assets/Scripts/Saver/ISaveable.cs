/// <summary>
/// Implemented by any system that wants to be part of a save (progression, inventory,
/// settings, ...). This is the Memento pattern: each system hands out an opaque snapshot
/// of itself (CaptureState) and can rebuild itself from one (RestoreState), so the save
/// service never needs to know what's actually inside.
/// </summary>
public interface ISaveable
{
    /// <summary>Stable, unique key used to file this system's data in the save (e.g. "progression").</summary>
    string SaveId { get; }

    /// <summary>Produce an independent snapshot of the current state.</summary>
    object CaptureState();

    /// <summary>Rebuild state from a previously captured snapshot.</summary>
    void RestoreState(object state);
}
