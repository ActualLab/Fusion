using System.Buffers.Binary;

namespace ActualLab.Collections;

public static partial class SpanExt
{
    // ReadLittleEndian

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadLittleEndian(this Span<byte> span)
        => BitConverter.IsLittleEndian
            ? span.ReadUnchecked<int>()
            : BinaryPrimitives.ReverseEndianness(span.ReadUnchecked<int>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadLittleEndian(this ReadOnlySpan<byte> span)
        => BitConverter.IsLittleEndian
            ? span.ReadUnchecked<int>()
            : BinaryPrimitives.ReverseEndianness(span.ReadUnchecked<int>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16LittleEndian(this Span<byte> span)
        => BitConverter.IsLittleEndian
            ? span.ReadUnchecked<ushort>()
            : BinaryPrimitives.ReverseEndianness(span.ReadUnchecked<ushort>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16LittleEndian(this ReadOnlySpan<byte> span)
        => BitConverter.IsLittleEndian
            ? span.ReadUnchecked<ushort>()
            : BinaryPrimitives.ReverseEndianness(span.ReadUnchecked<ushort>());

    // WriteLittleEndian

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLittleEndian(this Span<byte> span, int value)
    {
        if (!BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
        ref var byteRef = ref MemoryMarshal.GetReference(span);
        Unsafe.WriteUnaligned(ref byteRef, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLittleEndian(this Span<byte> span, ushort value)
    {
        if (!BitConverter.IsLittleEndian)
            value = BinaryPrimitives.ReverseEndianness(value);
        ref var byteRef = ref MemoryMarshal.GetReference(span);
        Unsafe.WriteUnaligned(ref byteRef, value);
    }
}
