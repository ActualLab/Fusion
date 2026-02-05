using System.Buffers.Binary;
using ActualLab.Internal;

namespace ActualLab.IO.Internal;

/// <summary>
/// A ref struct that sequentially reads binary primitives and length-prefixed spans
/// from a <see cref="ReadOnlyMemory{T}"/> of bytes.
/// </summary>
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
        var result = BinaryPrimitives.ReadUInt32LittleEndian(Remaining);
        Advance(4);
        return result;
    }

    public uint ReadNativeUInt32()
    {
        var result = Remaining.ReadUnchecked<uint>();
        Advance(4);
        return result;
    }

    public ulong ReadUInt64()
    {
        var result = BinaryPrimitives.ReadUInt64LittleEndian(Remaining);
        Advance(8);
        return result;
    }

    public ulong ReadNativeUInt64()
    {
        var result = Remaining.ReadUnchecked<ulong>();
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

    public ReadOnlySpan<byte> ReadL1Span()
    {
        var span = Remaining;
        var end = 1 + span[0];
        var result = span[1..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadL1Memory()
    {
        var start = Offset + 1;
        var end = start + Remaining[0];
        var result = Memory[start..end];
        Advance(end - start + 1);
        return result;
    }

    public ReadOnlySpan<byte> ReadL2Span()
    {
        var span = Remaining;
        var end = 2 + BinaryPrimitives.ReadUInt16LittleEndian(span);
        var result = span[2..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadL2Memory()
    {
        var start = Offset + 2;
        var end = start + BinaryPrimitives.ReadUInt16LittleEndian(Remaining);
        var result = Memory[start..end];
        Advance(end - start + 2);
        return result;
    }

    public ReadOnlySpan<byte> ReadNativeL2Span()
    {
        var span = Remaining;
        var end = 2 + Remaining.ReadUnchecked<ushort>();
        var result = span[2..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadNativeL2Memory()
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
        var size = BinaryPrimitives.ReadInt32LittleEndian(span);
        if (size < 0 || size > maxSize)
            throw Errors.SizeLimitExceeded();

        var end = size + 4;
        var result = span[4..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadL4Memory(int maxSize)
    {
        var size = BinaryPrimitives.ReadInt32LittleEndian(Remaining);
        if (size < 0 || size > maxSize)
            throw Errors.SizeLimitExceeded();

        var start = Offset + 4;
        var end = start + size;
        var result = Memory[start..end];
        Advance(size + 4);
        return result;
    }

    public ReadOnlySpan<byte> ReadNativeL4Span(uint maxSize)
    {
        var span = Remaining;
        var size = Remaining.ReadUnchecked<uint>();
        if (size > maxSize)
            throw Errors.SizeLimitExceeded();

        var end = (int)size + 4;
        var result = span[4..end];
        Advance(end);
        return result;
    }

    public ReadOnlyMemory<byte> ReadNativeL4Memory(uint maxSize)
    {
        var size = Remaining.ReadUnchecked<uint>();
        if (size > maxSize)
            throw Errors.SizeLimitExceeded();

        var start = Offset + 4;
        var end = start + (int)size;
        var result = Memory[start..end];
        Advance((int)size + 4);
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
