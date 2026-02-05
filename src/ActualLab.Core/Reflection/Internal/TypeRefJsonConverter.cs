namespace ActualLab.Reflection.Internal;

/// <summary>
/// System.Text.Json converter for <see cref="TypeRef"/>.
/// </summary>
public class TypeRefJsonConverter : JsonConverter<TypeRef>
{
    public override TypeRef Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, TypeRef value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.AssemblyQualifiedName);
}
