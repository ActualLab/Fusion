using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.IO.Internal;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Serialization;

public abstract class RpcArgumentSerializer
{
    [ThreadStatic] private static ArrayPoolBuffer<byte>? _writeBuffer;
    public static ArrayPool<byte> NoPool => NoArrayPool<byte>.Instance;
    public static int WriteBufferReplaceCapacity { get; set; } = 65536;
    public static int WriteBufferCapacity { get; set; } = 4096;
    public static int CopyThreshold { get; set; } = 1024;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool allowPolymorphism, int sizeHint);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void Deserialize(ref ArgumentList arguments, bool allowPolymorphism, ReadOnlyMemory<byte> data);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static ArrayPoolBuffer<byte> GetWriteBuffer(int sizeHint)
        => sizeHint >= CopyThreshold
            ? new ArrayPoolBuffer<byte>(NoPool, 256 + sizeHint, false)
            : ArrayPoolBuffer<byte>.NewOrReset(ref _writeBuffer, WriteBufferCapacity, WriteBufferReplaceCapacity, false);

    protected static ReadOnlyMemory<byte> GetWriteBufferMemory(ArrayPoolBuffer<byte> buffer)
    {
        var memory = buffer.WrittenMemory;
        if (ReferenceEquals(buffer.Pool, NoPool))
            return memory;

        if (memory.Length < CopyThreshold)
            return memory.ToArray();

        _writeBuffer = null;
        return memory;
    }

    protected static Type RequireNonAbstract(Type type)
    {
        if (type.IsAbstract)
            throw Errors.CannotSerializeAbstractType(type);

        return type;
    }
}
