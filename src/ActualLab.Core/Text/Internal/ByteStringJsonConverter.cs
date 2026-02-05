namespace ActualLab.Text.Internal;

/// <summary>
/// System.Text.Json converter for <see cref="ByteString"/>, using Base64 encoding.
/// </summary>
public class ByteStringJsonConverter : JsonConverter<ByteString>
{
    public override ByteString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ByteString.FromBase64(reader.GetString());

    public override void Write(Utf8JsonWriter writer, ByteString value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToBase64());
}
