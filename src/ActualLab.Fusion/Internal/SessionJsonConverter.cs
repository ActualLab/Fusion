namespace ActualLab.Fusion.Internal;

/// <summary>
/// A <see cref="JsonConverter{T}"/> that serializes <see cref="Session"/> as its string identifier.
/// </summary>
public class SessionJsonConverter : JsonConverter<Session?>
{
    public override Session? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value is null ? null : new Session(value);
    }

    public override void Write(Utf8JsonWriter writer, Session? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value?.Id);
}
