namespace ActualLab.Api.Internal;

/// <summary>
/// System.Text.Json converter factory for <see cref="ApiArray{T}"/>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Used constructors should be there for sure.")]
public class ApiArrayJsonConverter : JsonConverterFactory
{
    // TODO: Replace w/ GenericInstanceCache
    private static readonly ConcurrentDictionary<Type, JsonConverter?> ConverterCache = new();

    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ApiArray<>);

    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume JSON converter code is preserved")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => ConverterCache.GetOrAdd(typeToConvert, static t => {
            var canConvert = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ApiArray<>);
            if (!canConvert)
                return null;

            var tArg = t.GetGenericArguments()[0];
            var tConverter = typeof(Converter<>).MakeGenericType(tArg);
            return (JsonConverter)tConverter.CreateInstance();
        });

    // Nested types

    /// <summary>
    /// Typed System.Text.Json converter for <see cref="ApiArray{T}"/>.
    /// </summary>
    public sealed class Converter<T> : JsonConverter<ApiArray<T>>
    {
        [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume JSON converter code is preserved")]
        public override ApiArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var valueConverter = (JsonConverter<T>)options.GetConverter(typeof(T));
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException();

            var list = new List<T>();
            while (reader.Read()) {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return new(list);

                list.Add(valueConverter.Read(ref reader, typeof(T), options)!);
            }
            throw new JsonException();
        }

        [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume JSON converter code is preserved")]
        public override void Write(Utf8JsonWriter writer, ApiArray<T> value, JsonSerializerOptions options)
        {
            var valueConverter = (JsonConverter<T>)options.GetConverter(typeof(T));
            writer.WriteStartArray();
            foreach (var item in value.Items)
                valueConverter.Write(writer, item, options);
            writer.WriteEndArray();
        }
    }
}
