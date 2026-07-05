/// <summary>
/// Contract for "how data turns into text and back". A serializer only knows how to
/// convert objects to/from a string - it never touches files and never decides when
/// to save. Today it's JSON (JsonSaveSerializer); swapping to binary later just means
/// writing a different implementation of this interface.
/// </summary>
public interface ISerializer
{
    string Serialize(object data);
    T Deserialize<T>(string text);
}
