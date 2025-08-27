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

    public void WriteUInt(uint value)
    {
        for (var offset = 0; offset < 4; offset++) {
            Remaining[offset] = (byte)(value & 0xFF);
            value >>= 8;
        }
        Advance(4);
    }

    public void WriteULong(ulong value)
    {
        for (var offset = 0; offset < 8; offset++) {
            Remaining[offset] = (byte)(value & 0xFF);
            value >>= 8;
        }
        Advance(8);
    }

    public void WriteVarUInt(uint value)
    {
        var offset = 0;
        while (value >= 0x80) {
            Remaining[offset++] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }
        Remaining[offset++] = (byte)value;
        Advance(offset);
    }

    public void WriteVarULong(ulong value)
    {
        var offset = 0;
        while (value >= 0x80) {
            Remaining[offset++] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }
        Remaining[offset++] = (byte)value;
        Advance(offset);
    }

    public void WriteAltVarULong(ulong value)
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

    public void WriteL1Span(ReadOnlySpan<byte> source)
    {
        if (source.Length > 0xFF)
            throw new ArgumentOutOfRangeException(nameof(source), "Source length exceeds 255 bytes.");

        Remaining[0] = (byte)source.Length;
        source.CopyTo(Remaining[1..]);
        Advance(1 + source.Length);
    }

    public void WriteL2Span(ReadOnlySpan<byte> source)
    {
        if (source.Length > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(source), "Source length exceeds 65535 bytes.");

        Remaining.WriteUnchecked((ushort)source.Length);
        source.CopyTo(Remaining[2..]);
        Advance(2 + source.Length);
    }

    public void WriteL4Span(ReadOnlySpan<byte> source)
    {
        Remaining.WriteUnchecked(source.Length);
        source.CopyTo(Remaining[4..]);
        Advance(4 + source.Length);
    }

    public void WriteLVarSpan(ReadOnlySpan<byte> source)
    {
        WriteVarUInt((uint)source.Length);
        source.CopyTo(Remaining);
        Advance(source.Length);
    }
}
