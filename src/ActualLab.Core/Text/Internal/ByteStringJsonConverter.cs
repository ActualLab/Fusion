namespace ActualLab.Text.Internal;

public class ByteStringJsonConverter : JsonConverter<ByteString>
{
    public override ByteString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ByteString.FromBase64(reader.GetString());

    public override void Write(Utf8JsonWriter writer, ByteString value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToBase64());
}
