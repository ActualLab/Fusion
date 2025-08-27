using ActualLab.Internal;

namespace ActualLab.IO.Internal;

[StructLayout(LayoutKind.Auto)]
public ref struct MemoryReader(ReadOnlyMemory<byte> memory)
{
    public ReadOnlyMemory<byte> Memory = memory;
    public ReadOnlySpan<byte> Remaining = memory.Span;

    public int Offset {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Memory.Length - Remaining.Length;
    }

    public override string ToString()
        => $"{nameof(MemoryReader)} @ {Offset} / {Memory.Length}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
        => Remaining = Remaining.Slice(count);

    public uint ReadUInt()
    {
        var result = 0u;
        var offset = 0;
        for (var shift = 0; shift < 32; shift += 8)
            result |= (uint)Remaining[offset++] << shift;
        Advance(offset);
        return result;
    }

    public ulong ReadULong()
    {
        var result = 0ul;
        var offset = 0;
        for (var shift = 0; shift < 64; shift += 8)
            result |= (ulong)Remaining[offset++] << shift;
        Advance(offset);
        return result;
    }

    public uint ReadVarUInt()
    {
        var result = 0u;
        var offset = 0;
        var shift = 0;
        byte b;
        do {
            b = Remaining[offset++];
            result |= (uint)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        Advance(offset);
        return result;
    }

    public ulong ReadVarULong()
    {
        var result = 0ul;
        var offset = 0;
        var shift = 0;
        byte b;
        do {
            b = Remaining[offset++];
            result |= (ulong)(b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        Advance(offset);
        return result;
    }

    public ulong ReadAltVarULong()
    {
        var size = Remaining[0];
        var result = size switch {
            2 => Remaining.ReadUnchecked<ushort>(1),
            4 => Remaining.ReadUnchecked<uint>(1),
            8 => Remaining.ReadUnchecked<ulong>(1),
            _ => throw Errors.Format("Invalid message format."),
        };
        Advance(size + 1);
        return result;
    }

    public ReadOnlySpan<byte> ReadL1Span()
    {
        var end = 1 + Remaining.ReadUnchecked<byte>();
        var result = Remaining[1..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadL1Memory()
    {
        var start = Offset + 1;
        var end = start + Remaining.ReadUnchecked<byte>();
        var result = Memory[start..end];
        Advance(end - start + 1);
        return result;
    }

    public ReadOnlySpan<byte> ReadL2Span()
    {
        var end = 2 + Remaining.ReadUnchecked<ushort>();
        var result = Remaining[2..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadL2Memory()
    {
        var start = Offset + 2;
        var end = start + Remaining.ReadUnchecked<ushort>();
        var result = Memory[start..end];
        Advance(end - start + 2);
        return result;
    }

    public ReadOnlySpan<byte> ReadL4Span()
    {
        var end = 4 + Remaining.ReadUnchecked<int>();
        var result = Remaining[4..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadL4Memory()
    {
        var start = Offset + 4;
        var end = start + Remaining.ReadUnchecked<int>();
        var result = Memory[start..end];
        Advance(end - start + 4);
        return result;
    }

    public ReadOnlySpan<byte> ReadLVarSpan()
    {
        var size = (int)ReadVarUInt();
        if (size < 0)
            throw Errors.Format("Invalid message format.");

        var result = Remaining[..size];
        Advance(size);
        return result;
    }

    public ReadOnlyMemory<byte> ReadLVarMemory()
    {
        var size = (int)ReadVarUInt();
        if (size < 0)
            throw Errors.Format("Invalid message format.");

        var start = Offset;
        var result = Memory[start..(start + size)];
        Advance(size);
        return result;
    }
}
