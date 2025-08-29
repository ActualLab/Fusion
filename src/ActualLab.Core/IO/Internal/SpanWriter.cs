namespace ActualLab.Internal;

[StructLayout(LayoutKind.Auto)]
public ref struct SpanWriter(Span<byte> buffer)
{
    public Span<byte> Span = buffer;
    public Span<byte> Remaining = buffer;

    public int Position {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Span.Length - Remaining.Length;
    }

    public override string ToString()
        => $"{nameof(SpanWriter)} @ {Position} / {Span.Length}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
        => Remaining = Remaining.Slice(count);

    public void WriteUInt32(uint value)
    {
        for (var offset = 0; offset < 4; offset++) {
            Remaining[offset] = (byte)(value & 0xFF);
            value >>= 8;
        }
        Advance(4);
    }

    public void WriteUInt64(ulong value)
    {
        for (var offset = 0; offset < 8; offset++) {
            Remaining[offset] = (byte)(value & 0xFF);
            value >>= 8;
        }
        Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVarUInt32(uint value, int offset = 0)
        => Advance(Remaining.WriteVarUInt32(value, offset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVarUInt64(ulong value, int offset = 0)
        => Advance(Remaining.WriteVarUInt64(value, offset));

    public void WriteAltVarUInt64(ulong value)
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

    public void WriteL1Span(ReadOnlySpan<byte> span)
    {
        if (span.Length > 0xFF)
            throw new ArgumentOutOfRangeException(nameof(span), "Source length exceeds 255 bytes.");

        Remaining[0] = (byte)span.Length;
        span.CopyTo(Remaining[1..]);
        Advance(1 + span.Length);
    }

    public void WriteL2Span(ReadOnlySpan<byte> span)
    {
        if (span.Length > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(span), "Source length exceeds 65535 bytes.");

        Remaining.WriteUnchecked((ushort)span.Length);
        span.CopyTo(Remaining[2..]);
        Advance(2 + span.Length);
    }

    public void WriteL4Span(ReadOnlySpan<byte> span)
    {
        Remaining.WriteUnchecked(span.Length);
        Advance(4);
        span.CopyTo(Remaining);
        Advance(span.Length);
    }

    public void WriteLVarSpan(ReadOnlySpan<byte> span)
    {
        WriteVarUInt32((uint)span.Length);
        span.CopyTo(Remaining);
        Advance(span.Length);
    }
}
