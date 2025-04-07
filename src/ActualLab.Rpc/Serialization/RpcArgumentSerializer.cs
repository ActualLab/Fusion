using System.Buffers;
using ActualLab.Interception;
using ActualLab.IO;
using ActualLab.IO.Internal;

namespace ActualLab.Rpc.Serialization;

public abstract class RpcArgumentSerializer(bool forcePolymorphism)
{
    [ThreadStatic] private static ArrayPoolBuffer<byte>? _writeBuffer;
    protected static readonly ArrayPool<byte> NoPool = NoArrayPool<byte>.Instance;

    public static int WriteBufferReplaceCapacity { get; set; } = 65536;
    public static int WriteBufferCapacity { get; set; } = 4096;
    public static int CopyThreshold { get; set; } = 1024;

    public bool ForcePolymorphism { get; } = forcePolymorphism;

    public abstract ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool needsPolymorphism, int sizeHint);
    public abstract void Deserialize(ref ArgumentList arguments, bool needsPolymorphism, ReadOnlyMemory<byte> data);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static ArrayPoolBuffer<byte> GetWriteBuffer(int sizeHint)
        => sizeHint >= CopyThreshold
            ? new ArrayPoolBuffer<byte>(NoPool, 256 + sizeHint, false)
            : ArrayPoolBuffer<byte>.NewOrReset(ref _writeBuffer, WriteBufferCapacity, WriteBufferReplaceCapacity,
                false);

    protected static ReadOnlyMemory<byte> GetWriteBufferMemory(ArrayPoolBuffer<byte> buffer)
    {
        var memory = buffer.WrittenMemory;
        if (ReferenceEquals(buffer.Pool, NoPool))
            return memory; // This buffer isn't pooled, so it's safe to return its memory directly

        if (memory.Length < CopyThreshold)
            return memory.ToArray();

        _writeBuffer = null;
        return memory; // We don't copy the memory here, but also "release" the buffer
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPolymorphic(Type type)
        => type.IsAbstract || type == typeof(object);
}
