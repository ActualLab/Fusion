using Newtonsoft.Json;

namespace ActualLab.Compliance.Internal;

/// <summary>
/// Newtonsoft.Json converter for <see cref="SanitizedString{TSanitizer}"/> - reads and writes
/// the raw value as a plain JSON string, so the wire form matches a <see cref="string"/>.
/// </summary>
public class SanitizedStringNewtonsoftJsonConverter : Newtonsoft.Json.JsonConverter
{
    public override bool CanConvert(Type objectType)
        => objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(SanitizedString<>);

    public override void WriteJson(JsonWriter writer, object? value, Newtonsoft.Json.JsonSerializer serializer)
        // ToString() renders the masked form while sanitization is active, so the raw value is
        // taken through ISanitizedString - writing a masked value to the wire would be data loss.
        => writer.WriteValue(((ISanitizedString?)value)?.Value);

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume JSON converter code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume JSON converter code is preserved")]
    public override object ReadJson(
        JsonReader reader, Type objectType, object? existingValue, Newtonsoft.Json.JsonSerializer serializer)
        => objectType.CreateInstance((string?)reader.Value);
}
