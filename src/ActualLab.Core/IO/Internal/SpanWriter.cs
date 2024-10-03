namespace ActualLab.IO.Internal;

[StructLayout(LayoutKind.Auto)]
public ref struct SpanWriter(Span<byte> buffer)
{
    public Span<byte> Span = buffer;
    public Span<byte> Remaining = buffer;

    public int Offset {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Span.Length - Remaining.Length;
    }

    public override string ToString()
        => $"{nameof(SpanWriter)} @ {Offset} / {Span.Length}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
        => Remaining = Remaining.Slice(count);

    public void WriteVarULong(ulong value)
    {
        if (value <= 0xFFFF) {
            Remaining.WriteUnchecked(2);
            Remaining.WriteUnchecked(1, (ushort)value);
            Advance(3);
        }
        else if (value <= 0xFFFFFFFF) {
            Remaining.WriteUnchecked(4);
            Remaining.WriteUnchecked(1, (uint)value);
            Advance(5);
        }
        else {
            Remaining.WriteUnchecked(8);
            Remaining.WriteUnchecked(1, value);
            Advance(9);
        }
    }

    public void WriteSpanL1(ReadOnlySpan<byte> source)
    {
        if (source.Length > 0xFF)
            throw new ArgumentOutOfRangeException(nameof(source), "Source length exceeds 255 bytes.");

        Remaining[0] = (byte)source.Length;
        source.CopyTo(Remaining[1..]);
        Advance(1 + source.Length);
    }

    public void WriteSpanL2(ReadOnlySpan<byte> source)
    {
        if (source.Length > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(source), "Source length exceeds 65535 bytes.");

        Remaining.WriteUnchecked((ushort)source.Length);
        source.CopyTo(Remaining[2..]);
        Advance(2 + source.Length);
    }

    public void WriteSpanL4(ReadOnlySpan<byte> source)
    {
        Remaining.WriteUnchecked(source.Length);
        source.CopyTo(Remaining[4..]);
        Advance(4 + source.Length);
    }
}
