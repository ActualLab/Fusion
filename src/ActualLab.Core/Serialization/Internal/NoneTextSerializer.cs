using System.Buffers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Throws an error on any operation.
/// </summary>
public sealed class NoneTextSerializer : ITextSerializer
{
    public static readonly NoneTextSerializer Instance = new();

    public ITextSerializer Root => this;
    IByteSerializer IByteSerializer.Root => this;

    public bool PreferStringApi => false;

    public object? ReadUnscoped(string data, Type type)
        => throw Errors.NoSerializer();
    public object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength)
        => throw Errors.NoSerializer();
    public object? ReadUnscoped(ReadOnlyMemory<char> data, Type type)
        => throw Errors.NoSerializer();

    public string WriteUnscoped(object? value, Type type)
        => throw Errors.NoSerializer();
    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type)
        => throw Errors.NoSerializer();
    public void WriteUnscoped(TextWriter textWriter, object? value, Type type)
        => throw Errors.NoSerializer();

    IByteSerializer<T> IByteSerializer.ToTyped<T>(Type? serializedType)
        => ToTyped<T>(serializedType);
    public ITextSerializer<T> ToTyped<T>(Type? serializedType = null)
        => NoneTextSerializer<T>.Instance;
}

/// <summary>
/// Throws an error on any operation.
/// </summary>
public sealed class NoneTextSerializer<T> : ITextSerializer<T>
{
    public static readonly NoneTextSerializer<T> Instance = new();

    public ITextSerializer Root => NoneTextSerializer.Instance;
    IByteSerializer IByteSerializer<T>.Root => Root;

    public bool PreferStringApi => false;

    public T ReadUnscoped(string data)
        => throw Errors.NoSerializer();
    public T ReadUnscoped(ReadOnlyMemory<byte> data, out int readLength)
        => throw Errors.NoSerializer();
    public T ReadUnscoped(ReadOnlyMemory<char> data)
        => throw Errors.NoSerializer();

    public string WriteUnscoped(T value)
        => throw Errors.NoSerializer();
    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
        => throw Errors.NoSerializer();
    public void WriteUnscoped(TextWriter textWriter, T value)
        => throw Errors.NoSerializer();
}
