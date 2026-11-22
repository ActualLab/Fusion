using System.Buffers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Ignores any operation.
/// </summary>
public sealed class NullTextSerializer : ITextSerializer
{
    public static readonly NullTextSerializer Instance = new();

    public ITextSerializer Root => this;
    IByteSerializer IByteSerializer.Root => this;

    public bool PreferStringApi => false;

    // Read

    public object? ReadUnscoped(string data, Type type)
        => null;

    public object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        readLength = 0;
        return null;
    }

    public object? ReadUnscoped(ReadOnlyMemory<char> data, Type type)
        => null;

    // Write

    public string WriteUnscoped(object? value, Type type)
        => "";

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type)
    { }

    public void WriteUnscoped(TextWriter textWriter, object? value, Type type)
    { }

    // ToTyped

    IByteSerializer<T> IByteSerializer.ToTyped<T>(Type? serializedType)
        => ToTyped<T>(serializedType);

    public ITextSerializer<T> ToTyped<T>(Type? serializedType = null)
        => NullTextSerializer<T>.Instance;
}

/// <summary>
/// Ignores any operation.
/// </summary>
public sealed class NullTextSerializer<T> : ITextSerializer<T>
{
    public static readonly NullTextSerializer<T> Instance = new();

    public ITextSerializer Root => NullTextSerializer.Instance;
    IByteSerializer IByteSerializer<T>.Root => Root;

    public bool PreferStringApi => false;

    // Read

    public T ReadUnscoped(string data)
        => default!;

    public T ReadUnscoped(ReadOnlyMemory<byte> data, out int readLength)
    {
        readLength = 0;
        return default!;
    }

    public T ReadUnscoped(ReadOnlyMemory<char> data)
        => default!;

    // Write

    public string WriteUnscoped(T value)
        => "";

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
    { }

    public void WriteUnscoped(TextWriter textWriter, T value)
    { }
}
