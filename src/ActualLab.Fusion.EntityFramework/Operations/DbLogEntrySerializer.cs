namespace ActualLab.Fusion.EntityFramework.Operations;

/// <summary>
/// How the Operations Framework's own tables - <c>_Operations</c> and <c>_Events</c> - serialize
/// their payloads. Every such payload has both a text and a binary column, so changing
/// <see cref="Format"/> only changes what new rows use: the ones already written keep being read
/// from whichever column they landed in.
/// </summary>
/// <remarks>
/// It covers those two log tables only - application entities are none of its business.
/// <para>
/// A log entry can serialize itself without DI, so <see cref="Default"/> is what it falls back to.
/// Register an instance to override that:
/// <code>
/// services.AddSingleton(_ => DbLogEntrySerializer.Default with { Format = DataFormat.Text });
/// </code>
/// The registration is storage-wide rather than per-<c>DbContext</c>: a format is a property of the
/// schema, and <see cref="DbOperation"/> and <see cref="DbEvent"/> aren't generic over one.
/// </para>
/// </remarks>
public sealed record DbLogEntrySerializer
{
    // What a log entry built outside the Operations Framework's own pipeline falls back to.
    // Setting it is the no-DI way to change the default.
    public static DbLogEntrySerializer Default { get; set; } = new();

    // The one switch that moves both tables between their text and binary columns. It doesn't pick
    // a serializer - that's TextSerializer and ByteSerializer, changed independently of this.
    public DataFormat Format { get; init; } = DataFormat.Bytes;

    // For the *Data columns. Must be type-decorating: a payload is polymorphic - an ICommand, an
    // event's value - so the concrete type has to travel with it.
    public IByteSerializer ByteSerializer { get; init; } = MessagePackByteSerializer.DefaultTypeDecorating;

    // For the *Json columns. Must preserve a polymorphic payload's concrete type - Newtonsoft.Json's
    // default settings do this via TypeNameHandling.Auto.
    public ITextSerializer TextSerializer { get; init; } = NewtonsoftJsonSerializer.Default;

    // Serialization

    // T is a reference type because every payload here is polymorphic - the serializers are
    // type-decorating for exactly that reason
    public (string? Text, byte[]? Data) Serialize<T>(T? value)
        where T : class
    {
        if (Format is DataFormat.Text)
            return (TextSerializer.Write(value, typeof(T)), null);

        using var buffer = ByteSerializer.Write(value, typeof(T));
        return (null, buffer.WrittenSpan.ToArray());
    }

    // Reads whichever column carries the payload rather than the one Format names, so a row
    // written before the format was changed stays readable afterwards.
    public T? Deserialize<T>(string? text, byte[]? data)
        where T : class
    {
        if (data is { Length: > 0 }) {
            var memory = (ReadOnlyMemory<byte>)data;
            return ByteSerializer.Read<T>(ref memory);
        }

        return text.IsNullOrEmpty()
            ? default
            : (T?)TextSerializer.Read(text, typeof(T));
    }
}
