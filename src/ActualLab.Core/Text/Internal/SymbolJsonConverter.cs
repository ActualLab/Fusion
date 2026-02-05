namespace ActualLab.Text.Internal;

/// <summary>
/// System.Text.Json converter for <see cref="Symbol"/>.
/// </summary>
public class SymbolJsonConverter : JsonConverter<Symbol>
{
    public override Symbol Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, Symbol value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
