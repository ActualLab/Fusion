namespace ActualLab.Collections;

public static class SpanExt
{
    // AsSpanUnsafe

#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpanUnsafe<T>(this ReadOnlySpan<T> readOnlySpan)
#if NET9_0_OR_GREATER
        => Unsafe.As<ReadOnlySpan<T>, Span<T>>(ref readOnlySpan);
#else
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(readOnlySpan), readOnlySpan.Length);
#endif
#endif

    // ReadUnchecked

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadUnchecked<T>(this Span<byte> span)
    {
        ref var byteRef = ref MemoryMarshal.GetReference(span);
        return Unsafe.ReadUnaligned<T>(ref byteRef);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadUnchecked<T>(this Span<byte> span, int byteOffset)
    {
        ref var byteRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), byteOffset);
        return Unsafe.ReadUnaligned<T>(ref byteRef);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadUnchecked<T>(this ReadOnlySpan<byte> span)
    {
        ref var byteRef = ref MemoryMarshal.GetReference(span);
        return Unsafe.ReadUnaligned<T>(ref byteRef);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ReadUnchecked<T>(this ReadOnlySpan<byte> span, int byteOffset)
    {
        ref var byteRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), byteOffset);
        return Unsafe.ReadUnaligned<T>(ref byteRef);
    }

    // WriteUnchecked

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUnchecked<T>(this Span<byte> span, T value)
    {
        ref var byteRef = ref MemoryMarshal.GetReference(span);
        Unsafe.WriteUnaligned(ref byteRef, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUnchecked<T>(this Span<byte> span, int byteOffset, T value)
    {
        ref var byteRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), byteOffset);
        Unsafe.WriteUnaligned(ref byteRef, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUnchecked<T>(this ReadOnlySpan<byte> span, T value)
    {
        ref var byteRef = ref MemoryMarshal.GetReference(span);
        Unsafe.WriteUnaligned(ref byteRef, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteUnchecked<T>(this ReadOnlySpan<byte> span, int byteOffset, T value)
    {
        ref var byteRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), byteOffset);
        Unsafe.WriteUnaligned(ref byteRef, value);
    }

    // ReadVarXxx

    public static (uint Value, int Offset) ReadVarUInt(this ReadOnlySpan<byte> span, int offset = 0)
    {
        var result = 0u;
        var shift = 0;
        byte b;
        do {
            b = span[offset++];
            result |= (uint)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return (result, offset);
    }

    public static (ulong Value, int Offset) ReadVarULong(this ReadOnlySpan<byte> span, int offset = 0)
    {
        var result = 0ul;
        var shift = 0;
        byte b;
        do {
            b = span[offset++];
            result |= (ulong)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return (result, offset);
    }

    // WriteVarXxx

    public static int WriteVarUInt(this Span<byte> span, uint value, int offset = 0)
    {
        while (value >= 0x80) {
            span[offset++] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }
        span[offset++] = (byte)value;
        return offset;
    }

    public static int WriteVarULong(this Span<byte> span, ulong value, int offset = 0)
    {
        while (value >= 0x80) {
            span[offset++] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }
        span[offset++] = (byte)value;
        return offset;
    }
}
