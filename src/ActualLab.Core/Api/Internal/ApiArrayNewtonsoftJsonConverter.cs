using Newtonsoft.Json;
using JsonConverter = Newtonsoft.Json.JsonConverter;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Api.Internal;

#pragma warning disable CA1812

/// <summary>
/// Newtonsoft.Json converter for <see cref="ApiArray{T}"/>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Used constructors should be there for sure.")]
public class ApiArrayNewtonsoftJsonConverter : JsonConverter
{
    // TODO: Replace w/ GenericInstanceCache
    private static readonly ConcurrentDictionary<Type, JsonConverter?> ConverterCache = new();

    public override bool CanConvert(Type objectType)
        => GetConverter(objectType) is not null;

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        => GetConverter(objectType)!.ReadJson(reader, objectType, existingValue, serializer);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        => GetConverter(value!.GetType())!.WriteJson(writer, value, serializer);

    private static JsonConverter? GetConverter(Type type)
        => ConverterCache.GetOrAdd(type, static t => {
            var isNullable = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
            var canConvert = (isNullable && IsApiArray(t.GetGenericArguments()[0])) || IsApiArray(t);
            if (!canConvert)
                return null;

            if (isNullable) {
                var tNArg = t.GetGenericArguments()[0].GetGenericArguments()[0];
                var tNConverter = typeof(NullableConverter<>).MakeGenericType(tNArg);
                return (JsonConverter)tNConverter.CreateInstance();
            }
            var tArg = t.GetGenericArguments()[0];
            var tConverter = typeof(Converter<>).MakeGenericType(tArg);
            return (JsonConverter)tConverter.CreateInstance();
        });

    private static bool IsApiArray(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiArray<>);

    // Nested types

    /// <summary>
    /// Typed Newtonsoft.Json converter for <see cref="ApiArray{T}"/>.
    /// </summary>
    private sealed class Converter<T> : Newtonsoft.Json.JsonConverter<ApiArray<T>>
    {
        public override ApiArray<T> ReadJson(
            JsonReader reader,
            Type objectType,
            ApiArray<T> existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var items = serializer.Deserialize<T[]>(reader);
            return new(items!);
        }

        public override void WriteJson(JsonWriter writer, ApiArray<T> value, JsonSerializer serializer)
            => serializer.Serialize(writer, value.Items);
    }

    /// <summary>
    /// Newtonsoft.Json converter for nullable <see cref="ApiArray{T}"/>.
    /// </summary>
    private sealed class NullableConverter<T> : Newtonsoft.Json.JsonConverter<ApiArray<T>?>
    {
        public override ApiArray<T>? ReadJson(
            JsonReader reader,
            Type objectType,
            ApiArray<T>? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var items = serializer.Deserialize<T[]?>(reader);
            if (items is null)
                return null;

            return new(items);
        }

        public override void WriteJson(JsonWriter writer, ApiArray<T>? value, JsonSerializer serializer)
            => serializer.Serialize(writer, value?.Items);
    }
}
