using Newtonsoft.Json;

namespace ActualLab.Text.Internal;

public class ByteStringNewtonsoftJsonConverter : Newtonsoft.Json.JsonConverter<ByteString>
{
    public override void WriteJson(
        JsonWriter writer, ByteString value,
        Newtonsoft.Json.JsonSerializer serializer)
        => writer.WriteValue(value.ToBase64());

    public override ByteString ReadJson(
        JsonReader reader, Type objectType, ByteString existingValue, bool hasExistingValue,
        Newtonsoft.Json.JsonSerializer serializer)
        => ByteString.FromBase64((string?) reader.Value!);
}
