using Newtonsoft.Json;

namespace ActualLab.Reflection.Internal;

/// <summary>
/// Newtonsoft.Json converter for <see cref="TypeRef"/>.
/// </summary>
public class TypeRefNewtonsoftJsonConverter : Newtonsoft.Json.JsonConverter<TypeRef>
{
    public override void WriteJson(
        JsonWriter writer, TypeRef value,
        Newtonsoft.Json.JsonSerializer serializer)
        => writer.WriteValue(value.AssemblyQualifiedName);

    public override TypeRef ReadJson(
        JsonReader reader, Type objectType, TypeRef existingValue, bool hasExistingValue,
        Newtonsoft.Json.JsonSerializer serializer)
        => new((string?) reader.Value!);
}
