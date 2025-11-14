namespace ActualLab.Collections;

public static partial class SpanExt
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

    extension(Span<byte> span)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadUnchecked<T>()
        {
            ref var byteRef = ref MemoryMarshal.GetReference(span);
            return Unsafe.ReadUnaligned<T>(ref byteRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadUnchecked<T>(int byteOffset)
        {
            ref var byteRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), byteOffset);
            return Unsafe.ReadUnaligned<T>(ref byteRef);
        }
    }

    extension(ReadOnlySpan<byte> span)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadUnchecked<T>()
        {
            ref var byteRef = ref MemoryMarshal.GetReference(span);
            return Unsafe.ReadUnaligned<T>(ref byteRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadUnchecked<T>(int byteOffset)
        {
            ref var byteRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), byteOffset);
            return Unsafe.ReadUnaligned<T>(ref byteRef);
        }
    }

    // WriteUnchecked

    extension(Span<byte> span)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnchecked<T>(T value)
        {
            ref var byteRef = ref MemoryMarshal.GetReference(span);
            Unsafe.WriteUnaligned(ref byteRef, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnchecked<T>(T value, int byteOffset)
        {
            ref var byteRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), byteOffset);
            Unsafe.WriteUnaligned(ref byteRef, value);
        }
    }

    extension(ReadOnlySpan<byte> span)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnchecked<T>(T value)
        {
            ref var byteRef = ref MemoryMarshal.GetReference(span);
            Unsafe.WriteUnaligned(ref byteRef, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUnchecked<T>(T value, int byteOffset)
        {
            ref var byteRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), byteOffset);
            Unsafe.WriteUnaligned(ref byteRef, value);
        }
    }
}
