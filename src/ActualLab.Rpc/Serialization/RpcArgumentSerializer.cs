using ActualLab.Interception;

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
        // An array is as polymorphic as its elements: T[] is never abstract, so without this
        // an abstract T inside it would reach the base serializer undecorated, which only
        // works for types that opt out via [RpcSerializable] - and those return false here anyway.
        => type.IsArray
            ? IsPolymorphic(type.GetElementType()!)
            : (type.IsAbstract || type == typeof(object)) && RpcSerializableAttribute.Get(type) is null;
}
