using Newtonsoft.Json;
using JsonConverter = Newtonsoft.Json.JsonConverter;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Rpc.Serialization.Internal;

public class RpcStreamNewtonsoftJsonConverter : JsonConverter
{
    // TODO: Replace w/ GenericInstanceCache
    private static readonly ConcurrentDictionary<Type, JsonConverter?> ConverterCache = new();

    public override bool CanConvert(Type objectType)
        => GetConverter(objectType) != null;

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer)
        => GetConverter(objectType)!.ReadJson(reader, objectType, existingValue, serializer);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        => GetConverter(value!.GetType())!.WriteJson(writer, value, serializer);

    private static JsonConverter? GetConverter(Type type)
        => ConverterCache.GetOrAdd(type, static t => {
            var canConvert = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(RpcStream<>);
            if (!canConvert)
                return null;

            var tArg = t.GetGenericArguments()[0];
            var tConverter = typeof(Converter<>).MakeGenericType(tArg);
            return (JsonConverter)tConverter.CreateInstance();
        });

    // Nested types

    public class Converter<T> : Newtonsoft.Json.JsonConverter<RpcStream<T>>
    {
        public override void WriteJson(
            JsonWriter writer, RpcStream<T>? value,
            JsonSerializer serializer)
            => writer.WriteValue(RpcStream<T>.SerializeToString(value));

        public override RpcStream<T>? ReadJson(
            JsonReader reader, Type objectType, RpcStream<T>? existingValue, bool hasExistingValue,
            JsonSerializer serializer)
            => RpcStream<T>.DeserializeFromString((string?)reader.Value ?? "");
    }
}
