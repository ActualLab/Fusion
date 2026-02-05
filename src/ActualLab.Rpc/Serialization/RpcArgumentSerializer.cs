using ActualLab.Interception;
using ActualLab.IO;

namespace ActualLab.Rpc.Serialization;

/// <summary>
/// Base class for serializers that encode and decode RPC method argument lists.
/// </summary>
public abstract class RpcArgumentSerializer
{
    [ThreadStatic] private static ArrayPoolBuffer<byte>? _writeBuffer;

    public static int WriteBufferReplaceCapacity { get; set; } = 65536;
    public static int WriteBufferCapacity { get; set; } = 4096;
    public static int CopyThreshold { get; set; } = 1024;

    // Serializes arguments directly to the provided buffer
    public abstract void Serialize(ArgumentList arguments, bool needsPolymorphism, ArrayPoolBuffer<byte> buffer);
    public abstract void Deserialize(ref ArgumentList arguments, bool needsPolymorphism, ReadOnlyMemory<byte> data);

    // Gets a thread-local write buffer for cases where caller needs to serialize arguments independently
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPoolBuffer<byte> GetWriteBuffer()
        => ArrayPoolBuffer<byte>.NewOrRenew(ref _writeBuffer, WriteBufferCapacity, WriteBufferReplaceCapacity, false);

    // Gets the written memory from the buffer, handling copy-on-small-size for pooled buffers
    public static ReadOnlyMemory<byte> GetWriteBufferMemory(ArrayPoolBuffer<byte> buffer)
    {
        var memory = buffer.WrittenMemory;
        if (!ReferenceEquals(buffer, _writeBuffer))
            return memory; // This buffer isn't pooled, so it's safe to return its memory directly

        if (memory.Length <= CopyThreshold)
            return memory.ToArray();

        _writeBuffer = null;
        return memory; // We don't copy the memory here, but also "release" the buffer
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPolymorphic(Type type)
        => type.IsAbstract || type == typeof(object);
}
