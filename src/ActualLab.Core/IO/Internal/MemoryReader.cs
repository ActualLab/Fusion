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

    public ulong ReadVarULong()
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

    public ulong ReadVarULong(int offset)
    {
        var size = Remaining[offset++];
        var result = size switch {
            2 => Remaining.ReadUnchecked<ushort>(offset),
            4 => Remaining.ReadUnchecked<uint>(offset),
            8 => Remaining.ReadUnchecked<ulong>(offset),
            _ => throw Errors.Format("Invalid message format."),
        };
        Advance(size + offset);
        return result;
    }

    public ReadOnlySpan<byte> ReadSpanL1()
    {
        var end = 1 + Remaining.ReadUnchecked<byte>();
        var result = Remaining[1..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadMemoryL1()
    {
        var start = Offset + 1;
        var end = start + Remaining.ReadUnchecked<byte>();
        var result = Memory[start..end];
        Advance(end - start + 1);
        return result;
    }

    public ReadOnlySpan<byte> ReadSpanL2()
    {
        var end = 2 + Remaining.ReadUnchecked<ushort>();
        var result = Remaining[2..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadMemoryL2()
    {
        var start = Offset + 2;
        var end = start + Remaining.ReadUnchecked<ushort>();
        var result = Memory[start..end];
        Advance(end - start + 2);
        return result;
    }

    public ReadOnlySpan<byte> ReadSpanL4()
    {
        var end = 4 + Remaining.ReadUnchecked<int>();
        var result = Remaining[4..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadMemoryL4()
    {
        var start = Offset + 4;
        var end = start + Remaining.ReadUnchecked<int>();
        var result = Memory[start..end];
        Advance(end - start + 4);
        return result;
    }
}
