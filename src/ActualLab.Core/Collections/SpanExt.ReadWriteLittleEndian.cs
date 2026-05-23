using System.Buffers.Binary;

namespace ActualLab.Collections;

public static partial class SpanExt
{
    // ReadLittleEndian

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadLittleEndian(this Span<byte> span)
        => BitConverter.IsLittleEndian
            ? span.ReadUnchecked<int>()
            : BinaryPrimitives.ReadInt32LittleEndian(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadLittleEndian(this ReadOnlySpan<byte> span)
        => BitConverter.IsLittleEndian
            ? span.ReadUnchecked<int>()
            : BinaryPrimitives.ReadInt32LittleEndian(span);

    // WriteLittleEndian

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLittleEndian(this Span<byte> span, int value)
    {
        if (BitConverter.IsLittleEndian) {
            ref var byteRef = ref MemoryMarshal.GetReference(span);
            Unsafe.WriteUnaligned(ref byteRef, value);
        }
        else
            BinaryPrimitives.WriteInt32LittleEndian(span, value);
    }
}
