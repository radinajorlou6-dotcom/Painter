/// <summary>
/// Contract for "where the saved bytes live". Anything that can tell us whether a
/// save exists, read its text back, and write new text counts as storage.
/// Today we use the local file system (FileStorage), but a cloud/Steam backend
/// could implement this same interface without the rest of the game noticing.
/// </summary>
public interface IStorage
{
    bool Exists();
    string Read();
    void Write(string text);
}
