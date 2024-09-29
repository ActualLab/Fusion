namespace ActualLab.Serialization.Internal;

public class JsonStringJsonConverter : JsonConverter<JsonString>
{
    public override JsonString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonString.New(reader.GetString());

    public override void Write(Utf8JsonWriter writer, JsonString value, JsonSerializerOptions options)
        => writer.WriteStringValue(value?.Value);
}
