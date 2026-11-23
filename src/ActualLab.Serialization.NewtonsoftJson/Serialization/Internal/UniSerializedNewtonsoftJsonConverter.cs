using Newtonsoft.Json;
using JsonConverter = Newtonsoft.Json.JsonConverter;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Newtonsoft.Json converter for <see cref="UniSerialized{T}"/> and <see cref="TypeDecoratingUniSerialized{TSchema,T}"/>.
/// Both are written as <c>{"NewtonsoftJson":"..."}</c>, where the string is the value serialized
/// by the current Newtonsoft.Json serializer (see <see cref="ByteSerializer.Current"/>), type-decorated for the latter.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Used constructors should be there for sure.")]
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "Used constructors should be there for sure.")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Used constructors should be there for sure.")]
public class UniSerializedNewtonsoftJsonConverter : JsonConverter
{
    public const string PropertyName = "NewtonsoftJson";

    private static readonly ConcurrentDictionary<Type, Handler?> HandlerCache = new();

    // The module initializer registers it first; this only covers hosts that don't run it
    static UniSerializedNewtonsoftJsonConverter()
    {
        if (!SerializerKind.NewtonsoftJson.IsAvailable())
            NewtonsoftJsonSerialization.Register();
    }

    public override bool CanConvert(Type objectType)
        => GetHandler(objectType) is not null;

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var handler = GetHandler(value!.GetType())!;
        writer.WriteStartObject();
        writer.WritePropertyName(PropertyName);
        writer.WriteValue(handler.Write(value));
        writer.WriteEndObject();
    }

    public override object? ReadJson(
        JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var handler = GetHandler(objectType)!;
        if (reader.TokenType == JsonToken.Null)
            return Nullable.GetUnderlyingType(objectType) is null ? handler.Read(null) : null;
        if (reader.TokenType != JsonToken.StartObject)
            throw new JsonSerializationException($"Unexpected token while reading {objectType.GetName()}: {reader.TokenType}.");

        string? text = null;
        while (reader.Read() && reader.TokenType != JsonToken.EndObject) {
            var name = (string?)reader.Value;
            reader.Read();
            if (string.Equals(name, PropertyName, StringComparison.OrdinalIgnoreCase))
                text = reader.TokenType == JsonToken.Null ? null : (string?)reader.Value;
            else
                reader.Skip();
        }
        return handler.Read(text);
    }

    // Private methods

    private static Handler? GetHandler(Type type)
        => HandlerCache.GetOrAdd(type, static t => {
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (!t.IsGenericType)
                return null;

            var genericType = t.GetGenericTypeDefinition();
            var handlerType = genericType == typeof(UniSerialized<>) ? typeof(UniHandler<>)
                : genericType == typeof(TypeDecoratingUniSerialized<,>) ? typeof(TypeDecoratingUniHandler<,>)
                : null;
            return handlerType is null
                ? null
                : (Handler)handlerType.MakeGenericType(t.GetGenericArguments()).CreateInstance();
        });

    // Nested types

    private abstract class Handler
    {
        public abstract string Write(object value);
        // A missing or null property leaves the value at its default, as Newtonsoft.Json did
        // when it set the former NewtonsoftJson property of these structs.
        public abstract object Read(string? text);
    }

    private sealed class UniHandler<T> : Handler
    {
        public override string Write(object value)
            => GetSerializer().Write(((UniSerialized<T>)value).Value, typeof(T));

        public override object Read(string? text)
            => text is null
                ? default(UniSerialized<T>)
                : new UniSerialized<T> { Value = (T)GetSerializer().Read(text, typeof(T))! };

        private static ITextSerializer GetSerializer()
            => ByteSerializer.GetCurrent<NewtonsoftJsonSerializer>();
    }

    private sealed class TypeDecoratingUniHandler<TSchema, T> : Handler
        where TSchema : TypeSchema, new()
    {
        public override string Write(object value)
            => GetSerializer().Write(((TypeDecoratingUniSerialized<TSchema, T>)value).Value, typeof(T));

        public override object Read(string? text)
            => text is null
                ? default(TypeDecoratingUniSerialized<TSchema, T>)
                : new TypeDecoratingUniSerialized<TSchema, T> { Value = (T)GetSerializer().Read(text, typeof(T))! };

        private static ITextSerializer GetSerializer()
            => (ITextSerializer)TypeSchema<TSchema>.GetTypeDecoratingSerializer(
                ByteSerializer.GetCurrent<NewtonsoftJsonSerializer>(), SerializerKind.NewtonsoftJson);
    }
}
