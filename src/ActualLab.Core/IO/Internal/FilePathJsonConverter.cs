namespace ActualLab.IO.Internal;

/// <summary>
/// System.Text.Json converter for <see cref="FilePath"/>.
/// </summary>
public class FilePathJsonConverter : JsonConverter<FilePath>
{
    public override FilePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString());

    public override void Write(Utf8JsonWriter writer, FilePath value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
