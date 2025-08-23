using System.Buffers;

namespace ActualLab.Serialization.Internal;

public sealed class CastingTextSerializer<T>(
    ITextSerializer untypedSerializer,
    Type serializedType
    ) : ITextSerializer<T>
{
    public ITextSerializer UntypedSerializer { get; } = untypedSerializer;
    public Type SerializedType { get; } = serializedType;
    public bool PreferStringApi => UntypedSerializer.PreferStringApi;

    public T Read(string data)
        => (T) UntypedSerializer.Read(data, SerializedType)!;
    public T Read(in ReadOnlyMemory<byte> data, out int readLength)
        => (T) UntypedSerializer.Read(data, SerializedType, out readLength)!;
    public T Read(ReadOnlyMemory<char> data)
        => (T) UntypedSerializer.Read(data, SerializedType)!;

    public string Write(T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => UntypedSerializer.Write(value, SerializedType);
    public void Write(IBufferWriter<byte> bufferWriter, T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => UntypedSerializer.Write(bufferWriter, value, SerializedType);
    public void Write(TextWriter textWriter, T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => UntypedSerializer.Write(textWriter, value, SerializedType);
}
