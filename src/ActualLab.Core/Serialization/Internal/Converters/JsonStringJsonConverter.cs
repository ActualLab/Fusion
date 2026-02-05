namespace ActualLab.Serialization.Internal;

/// <summary>
/// A System.Text.Json converter for <see cref="JsonString"/>.
/// </summary>
public class JsonStringJsonConverter : JsonConverter<JsonString>
{
    public override JsonString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonString.New(reader.GetString());

    public override void Write(Utf8JsonWriter writer, JsonString value, JsonSerializerOptions options)
        => writer.WriteStringValue(value?.Value);
}
