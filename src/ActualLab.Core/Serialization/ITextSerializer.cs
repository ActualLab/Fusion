namespace ActualLab.Serialization;

/// <summary>
/// Defines a contract for text-based serialization, extending <see cref="IByteSerializer"/>
/// with string and char-memory read/write support.
/// </summary>
public interface ITextSerializer : IByteSerializer
{
    public new ITextSerializer Root { get; }
    public bool PreferStringApi { get; }

    // See IByteSerializer.ReadUnscoped
    public object? ReadUnscoped(string data, Type type);
    public object? ReadUnscoped(ReadOnlyMemory<char> data, Type type);
    public string WriteUnscoped(object? value, Type type);
    public void WriteUnscoped(TextWriter textWriter, object? value, Type type);

    public new ITextSerializer<T> ToTyped<T>(Type? serializedType = null);
}

/// <summary>
/// Defines a contract for text-based serialization of <typeparamref name="T"/>.
/// </summary>
public interface ITextSerializer<T> : IByteSerializer<T>
{
    public bool PreferStringApi { get; }
    public new ITextSerializer Root { get; }

    // See IByteSerializer.ReadUnscoped
    public T ReadUnscoped(string data);
    public T ReadUnscoped(ReadOnlyMemory<char> data);
    public string WriteUnscoped(T value);
    public void WriteUnscoped(TextWriter textWriter, T value);
}
