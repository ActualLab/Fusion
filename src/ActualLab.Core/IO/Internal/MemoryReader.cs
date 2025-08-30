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
        var span = Remaining;
        var result = BitConverter.IsLittleEndian
            ? span.ReadUnchecked<uint>(0)
            : span[0]
                | ((uint)span[1] << 8)
                | ((uint)span[2] << 16)
                | ((uint)span[3] << 24);
        Advance(4);
        return result;
    }

    public ulong ReadUInt64()
    {
        var span = Remaining;
        var result = BitConverter.IsLittleEndian
            ? span.ReadUnchecked<ulong>(0)
            : span[0]
                | ((ulong)span[1] << 8)
                | ((ulong)span[2] << 16)
                | ((ulong)span[3] << 24)
                | ((ulong)span[4] << 32)
                | ((ulong)span[5] << 40)
                | ((ulong)span[6] << 48)
                | ((ulong)span[7] << 56);
        Advance(8);
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
        var span = Remaining;
        var size = span[0];
        var result = size switch {
            2 => span.ReadUnchecked<ushort>(1),
            4 => span.ReadUnchecked<uint>(1),
            8 => span.ReadUnchecked<ulong>(1),
            _ => throw Errors.Format("Invalid message format."),
        };
        Advance(size + 1);
        return result;
    }

    public ReadOnlySpan<byte> ReadL1Span()
    {
        var span = Remaining;
        var end = 1 + span.ReadUnchecked<byte>();
        var result = span[1..end];
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
        var span = Remaining;
        var end = 2 + span.ReadUnchecked<ushort>();
        var result = span[2..end];
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
        var span = Remaining;
        var size = span.ReadUnchecked<int>();
        if (size < 0 || size > maxSize)
            throw Errors.SizeLimitExceeded();

        var end = size + 4;
        var result = span[4..end];
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
