using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Serialization.Internal;

public sealed class CastingByteSerializer<T>(IByteSerializer untypedSerializer, Type serializedType)
    : IByteSerializer<T>
{
    public IByteSerializer UntypedSerializer { get; } = untypedSerializer;
    public Type SerializedType { get; } = serializedType;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public T Read(ReadOnlyMemory<byte> data, out int readLength)
        => (T) UntypedSerializer.Read(data, SerializedType, out readLength)!;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public void Write(IBufferWriter<byte> bufferWriter, T value)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => UntypedSerializer.Write(bufferWriter, value, SerializedType);
}
