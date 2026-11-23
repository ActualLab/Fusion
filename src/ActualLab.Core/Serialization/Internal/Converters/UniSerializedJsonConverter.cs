namespace ActualLab.Serialization.Internal;

/// <summary>
/// A System.Text.Json converter for <see cref="UniSerialized{T}"/> and <see cref="TypeDecoratingUniSerialized{TSchema,T}"/>:
/// a <c>{"json":"..."}</c> object holding the value serialized by the current System.Text.Json serializer.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "We assume serializable types are fully preserved")]
public class UniSerializedJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
            return false;

        var genericType = typeToConvert.GetGenericTypeDefinition();
        return genericType == typeof(UniSerialized<>) || genericType == typeof(TypeDecoratingUniSerialized<,>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var arguments = typeToConvert.GetGenericArguments();
        var converterType = typeToConvert.GetGenericTypeDefinition() == typeof(UniSerialized<>)
            ? typeof(Converter<>).MakeGenericType(arguments)
            : typeof(TypeDecoratingConverter<,>).MakeGenericType(arguments);
        // Not ActivatorExt.CreateInstance: NativeAOT can't build a constructor delegate for a new closed type
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    // Private methods

    private static string GetPropertyName(JsonSerializerOptions options)
        => options.PropertyNamingPolicy?.ConvertName("Json") ?? "Json";

    private static void WriteEnvelope(Utf8JsonWriter writer, string payload, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(GetPropertyName(options), payload);
        writer.WriteEndObject();
    }

    private static string? ReadEnvelope(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected a JSON object.");

        var propertyName = GetPropertyName(options);
        var comparison = options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string? payload = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
            var name = reader.GetString();
            reader.Read();
            if (string.Equals(name, propertyName, comparison))
                payload = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            else
                reader.Skip();
        }
        return payload;
    }

    // Nested types

    private sealed class Converter<T> : JsonConverter<UniSerialized<T>>
    {
        public override void Write(Utf8JsonWriter writer, UniSerialized<T> value, JsonSerializerOptions options)
            => WriteEnvelope(writer, GetSerializer().Write(value.Value, typeof(T)), options);

        public override UniSerialized<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => ReadEnvelope(ref reader, options) is { } payload
                ? new UniSerialized<T> { Value = (T)GetSerializer().Read(payload, typeof(T))! }
                : default;

        private static ITextSerializer GetSerializer()
            => ByteSerializer.GetCurrent<SystemJsonSerializer>();
    }

    private sealed class TypeDecoratingConverter<TSchema, T> : JsonConverter<TypeDecoratingUniSerialized<TSchema, T>>
        where TSchema : TypeSchema
    {
        public override void Write(
            Utf8JsonWriter writer, TypeDecoratingUniSerialized<TSchema, T> value, JsonSerializerOptions options)
            => WriteEnvelope(writer, GetSerializer().Write(value.Value, typeof(T)), options);

        public override TypeDecoratingUniSerialized<TSchema, T> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => ReadEnvelope(ref reader, options) is { } payload
                ? new TypeDecoratingUniSerialized<TSchema, T> { Value = (T)GetSerializer().Read(payload, typeof(T))! }
                : default;

        private static ITextSerializer GetSerializer()
        {
            var serializer = ByteSerializer.GetCurrent<SystemJsonSerializer>();
            return (ITextSerializer)TypeSchema<TSchema>.GetTypeDecoratingSerializer(serializer, SerializerKind.SystemJson);
        }
    }
}
