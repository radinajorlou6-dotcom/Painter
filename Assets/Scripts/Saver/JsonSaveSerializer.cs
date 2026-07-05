using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

/// <summary>
/// JSON implementation of ISerializer, backed by Newtonsoft.
///
/// TypeNameHandling.Auto is important here: the SaveFile stores each system's snapshot
/// as an 'object'. Auto embeds a small "$type" tag so that when we read the file back,
/// each snapshot is reconstructed as its real type (e.g. GameData) instead of a generic
/// blob. That's what lets ISaveable.RestoreState receive a real, castable object.
///
/// Note: TypeNameHandling can be a security risk when deserializing UNTRUSTED data.
/// That's fine here because we only ever read our own local save file.
/// </summary>
public class JsonSaveSerializer : ISerializer
{
    private readonly JsonSerializerSettings settings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Auto,
        // Store enums by name ("Slingshot") rather than number (0). Reordering the enum later
        // then can't silently remap old saves to the wrong value.
        Converters = { new StringEnumConverter() }
    };

    public string Serialize(object data) => JsonConvert.SerializeObject(data, settings);

    public T Deserialize<T>(string text) => JsonConvert.DeserializeObject<T>(text, settings);
}
