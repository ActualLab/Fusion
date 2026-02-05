namespace ActualLab.Serialization;

/// <summary>
/// Defines a contract for text-based serialization, extending <see cref="IByteSerializer"/>
/// with string and char-memory read/write support.
/// </summary>
public interface ITextSerializer : IByteSerializer
{
    public bool PreferStringApi { get; }

    public object? Read(string data, Type type);
    public object? Read(ReadOnlyMemory<char> data, Type type);
    public string Write(object? value, Type type);
    public void Write(TextWriter textWriter, object? value, Type type);

    public new ITextSerializer<T> ToTyped<T>(Type? serializedType = null);
}

/// <summary>
/// Defines a contract for text-based serialization of <typeparamref name="T"/>.
/// </summary>
public interface ITextSerializer<T> : IByteSerializer<T>
{
    public bool PreferStringApi { get; }

    public T Read(string data);
    public T Read(ReadOnlyMemory<char> data);
    public string Write(T value);
    public void Write(TextWriter textWriter, T value);
}
