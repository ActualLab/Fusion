using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Serialization;

public interface IByteSerializer
{
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    void Write(IBufferWriter<byte> bufferWriter, object? value, Type type);
    IByteSerializer<T> ToTyped<T>(Type? serializedType = null);
}

public interface IByteSerializer<T>
{
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    T Read(ReadOnlyMemory<byte> data, out int readLength);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    void Write(IBufferWriter<byte> bufferWriter, T value);
}

public interface IProjectingByteSerializer<T> : IByteSerializer<T>
{
    bool AllowProjection { get; }

    public T Read(ReadOnlyMemory<byte> data, out int readLength, out bool isProjection);
}
