using Newtonsoft.Json;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A Newtonsoft.Json converter for <see cref="JsonString"/>.
/// </summary>
public class JsonStringNewtonsoftJsonConverter : Newtonsoft.Json.JsonConverter<JsonString>
{
    public override void WriteJson(
        JsonWriter writer, JsonString? value,
        Newtonsoft.Json.JsonSerializer serializer)
        => writer.WriteValue(value?.Value);

    public override JsonString? ReadJson(
        JsonReader reader, Type objectType, JsonString? existingValue, bool hasExistingValue,
        Newtonsoft.Json.JsonSerializer serializer)
        => JsonString.New((string?) reader.Value);
}
