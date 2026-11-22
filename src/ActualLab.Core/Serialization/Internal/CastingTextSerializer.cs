using System.Buffers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A typed <see cref="ITextSerializer{T}"/> adapter that delegates to an untyped <see cref="ITextSerializer"/>.
/// </summary>
public sealed class CastingTextSerializer<T>(
    ITextSerializer untypedSerializer,
    Type serializedType
    ) : ITextSerializer<T>
{
    public ITextSerializer UntypedSerializer { get; } = untypedSerializer;
    public Type SerializedType { get; } = serializedType;
    public ITextSerializer Root { get; } = untypedSerializer.Root;
    IByteSerializer IByteSerializer<T>.Root => Root;
    public bool PreferStringApi => UntypedSerializer.PreferStringApi;

    public T ReadUnscoped(string data)
        => (T)UntypedSerializer.ReadUnscoped(data, SerializedType)!;

    public T ReadUnscoped(ReadOnlyMemory<byte> data, out int readLength)
        => (T)UntypedSerializer.ReadUnscoped(data, SerializedType, out readLength)!;

    public T ReadUnscoped(ReadOnlyMemory<char> data)
        => (T)UntypedSerializer.ReadUnscoped(data, SerializedType)!;

    public string WriteUnscoped(T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => UntypedSerializer.WriteUnscoped(value, SerializedType);

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => UntypedSerializer.WriteUnscoped(bufferWriter, value, SerializedType);

    public void WriteUnscoped(TextWriter textWriter, T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => UntypedSerializer.WriteUnscoped(textWriter, value, SerializedType);
}
