namespace ActualLab.Rpc.Serialization.Internal;

public class RpcStreamJsonConverter : JsonConverterFactory
{
    // TODO: Replace w/ GenericInstanceCache
    private static readonly ConcurrentDictionary<Type, JsonConverter?> ConverterCache = new();

    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(RpcStream<>);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => ConverterCache.GetOrAdd(typeToConvert, static t => {
            var canConvert = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(RpcStream<>);
            if (!canConvert)
                return null;

            var tArg = t.GetGenericArguments()[0];
            var tConverter = typeof(Converter<>).MakeGenericType(tArg);
            return (JsonConverter)tConverter.CreateInstance();
        });

    // Nested type

    public class Converter<T> : JsonConverter<RpcStream<T>>
    {
        public override RpcStream<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => RpcStream<T>.DeserializeFromString(reader.GetString() ?? "");

        public override void Write(Utf8JsonWriter writer, RpcStream<T>? value, JsonSerializerOptions options)
            => writer.WriteStringValue(RpcStream<T>.SerializeToString(value));
    }
}
