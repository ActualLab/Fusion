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

    public uint ReadUInt32()
    {
        var result = 0u;
        var offset = 0;
        for (var shift = 0; shift < 32; shift += 8)
            result |= (uint)Remaining[offset++] << shift;
        Advance(offset);
        return result;
    }

    public ulong ReadUInt64()
    {
        var result = 0ul;
        var offset = 0;
        for (var shift = 0; shift < 64; shift += 8)
            result |= (ulong)Remaining[offset++] << shift;
        Advance(offset);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadVarUInt32()
    {
        var (value, size) = Remaining.ReadVarUInt32();
        Advance(size);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadVarUInt64()
    {
        var (value, size) = Remaining.ReadVarUInt64();
        Advance(size);
        return value;
    }

    public ulong ReadAltVarUInt64()
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

    public ReadOnlySpan<byte> ReadL4Span(int maxSize)
    {
        var size = Remaining.ReadUnchecked<int>();
        if (size < 0 || size > maxSize)
            throw Errors.SizeLimitExceeded();

        var end = size + 4;
        var result = Remaining[4..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadL4Memory(int maxSize)
    {
        var size = Remaining.ReadUnchecked<int>();
        if (size < 0 || size > maxSize)
            throw Errors.SizeLimitExceeded();

        var start = Offset + 4;
        var end = start + size;
        var result = Memory[start..end];
        Advance(size + 4);
        return result;
    }

    public ReadOnlySpan<byte> ReadLVarSpan(int maxSize)
    {
        var size = (int)ReadVarUInt32();
        if (size < 0 || size > maxSize)
            throw Errors.SizeLimitExceeded();

        var result = Remaining[..size];
        Advance(size);
        return result;
    }

    public ReadOnlyMemory<byte> ReadLVarMemory(int maxSize)
    {
        var size = (int)ReadVarUInt32();
        if (size < 0 || size > maxSize)
            throw Errors.SizeLimitExceeded();

        var start = Offset;
        var result = Memory[start..(start + size)];
        Advance(size);
        return result;
    }
}
