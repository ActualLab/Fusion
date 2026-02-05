namespace ActualLab.Time.Internal;

/// <summary>
/// System.Text.Json converter for <see cref="Moment"/>.
/// </summary>
public class MomentJsonConverter : JsonConverter<Moment>
{
    public override Moment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Moment.Parse(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, Moment value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
