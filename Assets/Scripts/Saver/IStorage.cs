public interface IStorage
{
    bool Exists();
    string ReadAllText();
    void WriteAllText(string text);
}