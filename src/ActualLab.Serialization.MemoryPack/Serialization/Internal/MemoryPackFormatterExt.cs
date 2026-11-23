using System.Buffers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Writer and reader helpers for the version-tolerant single-member objects the MemoryPack
/// generator emits without a temporary buffer, since the member's byte length is known upfront.
/// </summary>
public static class MemoryPackFormatterExt
{
    public static void WriteStringObject<TBufferWriter>(this ref MemoryPackWriter<TBufferWriter> writer, string? value)
#if NET7_0_OR_GREATER
        where TBufferWriter : IBufferWriter<byte>
#else
        where TBufferWriter : class, IBufferWriter<byte>
#endif
    {
        writer.WriteObjectHeader(1);
        writer.WriteVarInt(writer.GetStringWriteLength(value));
        writer.WriteString(value);
    }

    public static void WriteBytesObject<TBufferWriter>(this ref MemoryPackWriter<TBufferWriter> writer, byte[]? value)
#if NET7_0_OR_GREATER
        where TBufferWriter : IBufferWriter<byte>
#else
        where TBufferWriter : class, IBufferWriter<byte>
#endif
    {
        writer.WriteObjectHeader(1);
        writer.WriteVarInt(writer.GetUnmanageArrayWriteLength(value));
        writer.WriteUnmanagedArray(value);
    }

    public static void WriteBytesObject<TBufferWriter>(
        this ref MemoryPackWriter<TBufferWriter> writer, ReadOnlySpan<byte> value)
#if NET7_0_OR_GREATER
        where TBufferWriter : IBufferWriter<byte>
#else
        where TBufferWriter : class, IBufferWriter<byte>
#endif
    {
        writer.WriteObjectHeader(1);
        writer.WriteVarInt(value.Length + 4); // The collection header + the bytes
        writer.WriteUnmanagedSpan(value);
    }

    // Returns false for a null object header
    public static bool TryReadStringObject(this ref MemoryPackReader reader, out string? value)
    {
        value = null;
        if (!reader.TryReadObjectHeader(out var count))
            return false;
        if (count == 0)
            return true;

        Span<int> deltas = stackalloc int[count];
        for (var i = 0; i < count; i++)
            deltas[i] = reader.ReadVarIntInt32();
        if (deltas[0] != 0)
            value = reader.ReadString();
        for (var i = 1; i < count; i++)
            reader.Advance(deltas[i]);
        return true;
    }

    // Returns false for a null object header
    public static bool TryReadBytesObject(this ref MemoryPackReader reader, out byte[]? value)
    {
        value = null;
        if (!reader.TryReadObjectHeader(out var count))
            return false;
        if (count == 0)
            return true;

        Span<int> deltas = stackalloc int[count];
        for (var i = 0; i < count; i++)
            deltas[i] = reader.ReadVarIntInt32();
        if (deltas[0] != 0)
            value = reader.ReadUnmanagedArray<byte>();
        for (var i = 1; i < count; i++)
            reader.Advance(deltas[i]);
        return true;
    }
}
