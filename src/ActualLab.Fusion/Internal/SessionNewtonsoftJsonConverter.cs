using Newtonsoft.Json;

namespace ActualLab.Fusion.Internal;

/// <summary>
/// A Newtonsoft.Json converter that serializes <see cref="Session"/> as its string identifier.
/// </summary>
public class SessionNewtonsoftJsonConverter : Newtonsoft.Json.JsonConverter<Session>
{
    public override void WriteJson(
        JsonWriter writer, Session? value,
        Newtonsoft.Json.JsonSerializer serializer)
        => writer.WriteValue(value?.Id);

    public override Session? ReadJson(
        JsonReader reader, Type objectType, Session? existingValue, bool hasExistingValue,
        Newtonsoft.Json.JsonSerializer serializer)
    {
        var value = (string?) reader.Value;
        return value is null ? null : new Session(value);
    }
}
