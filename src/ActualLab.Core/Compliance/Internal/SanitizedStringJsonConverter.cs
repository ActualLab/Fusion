namespace ActualLab.Compliance.Internal;

/// <summary>
/// System.Text.Json converter factory for <see cref="SanitizedString{TSanitizer}"/>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Used constructors should be there for sure.")]
public class SanitizedStringJsonConverter : JsonConverterFactory
{
    private static readonly ConcurrentDictionary<Type, JsonConverter?> ConverterCache = new();

    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(SanitizedString<>);

    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume JSON converter code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume JSON converter code is preserved")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => ConverterCache.GetOrAdd(typeToConvert, static t => {
            if (!(t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SanitizedString<>)))
                return null;

            var tConverter = typeof(Converter<>).MakeGenericType(t.GetGenericArguments()[0]);
            return (JsonConverter)tConverter.CreateInstance();
        });

    // Nested types

    /// <summary>Typed converter - writes the raw value, exactly as a string would be written.</summary>
    public sealed class Converter<TSanitizer> : JsonConverter<SanitizedString<TSanitizer>>
        where TSanitizer : Sanitizer, new()
    {
        public override SanitizedString<TSanitizer> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString());

        public override void Write(
            Utf8JsonWriter writer, SanitizedString<TSanitizer> value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);

        public override SanitizedString<TSanitizer> ReadAsPropertyName(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetString());

        public override void WriteAsPropertyName(
            Utf8JsonWriter writer, SanitizedString<TSanitizer> value, JsonSerializerOptions options)
            => writer.WritePropertyName(value.Value);
    }
}
