namespace ActualLab.IO.Internal;

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
        var span = Remaining;
        if (BitConverter.IsLittleEndian)
            span.WriteUnchecked(value);
        else {
            span[0] = (byte)value;
            span[1] = (byte)(value >> 8);
            span[2] = (byte)(value >> 16);
            span[3] = (byte)(value >> 24);
        }
        Advance(4);
    }

    public void WriteUInt64(ulong value)
    {
        var span = Remaining;
        if (BitConverter.IsLittleEndian)
            span.WriteUnchecked(value);
        else {
            span[0] = (byte)value;
            span[1] = (byte)(value >> 8);
            span[2] = (byte)(value >> 16);
            span[3] = (byte)(value >> 24);
            span[4] = (byte)(value >> 32);
            span[5] = (byte)(value >> 40);
            span[6] = (byte)(value >> 48);
            span[7] = (byte)(value >> 56);
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
        var span = Remaining;
        if (value <= 0xFFFF) {
            span.WriteUnchecked(2);
            span.WriteUnchecked((ushort)value, 1);
            Advance(3);
        }
        else if (value <= 0xFFFFFFFF) {
            span.WriteUnchecked(4);
            span.WriteUnchecked((uint)value, 1);
            Advance(5);
        }
        else {
            span.WriteUnchecked(8);
            span.WriteUnchecked(value, 1);
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
