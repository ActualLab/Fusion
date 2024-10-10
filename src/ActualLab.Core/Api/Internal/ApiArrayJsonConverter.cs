namespace ActualLab.Api.Internal;

#pragma warning disable IL2026

public class ApiArrayJsonConverter : JsonConverterFactory
{
    private static readonly ConcurrentDictionary<Type, JsonConverter?> ConverterCache = new();

    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ApiArray<>);

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

    public sealed class Converter<T> : JsonConverter<ApiArray<T>>
    {
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
