#if NETCOREAPP3_1_OR_GREATER
using System.Numerics;
using System.Runtime.Intrinsics.X86;
#endif
using ActualLab.Internal;

namespace ActualLab.Collections;

public static partial class SpanExt
{
    public const int FixedLVarInt32Size = 5; // Fixed-width (non-minimal) LEB128 length prefix

    private const byte LowBits = 0x7F;
    private const byte HighBit = 0x80;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteVarUInt32(this Span<byte> span, uint source, int offset = 0)
    {
        if (source < 1u << 7) {
            span[offset] = (byte)source;
            return offset + 1;
        }

        span[offset++] = (byte)(HighBit | source);
        source >>= 7;
        if (source < 1u << 7) {
            span[offset] = (byte)source;
            return offset + 1;
        }

        span[offset++] = (byte)(HighBit | source);
        source >>= 7;
        if (source < 1u << 7) {
            span[offset] = (byte)source;
            return offset + 1;
        }

        while (source >= HighBit) {
            span[offset++] = (byte)(HighBit | source);
            source >>= 7;
        }
        span[offset++] = (byte)source;
        return offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteVarUInt64(this Span<byte> span, ulong source, int offset = 0)
    {
        if (source < 1ul << 7) {
            span[offset] = (byte)source;
            return offset + 1;
        }

        span[offset++] = (byte)(HighBit | source);
        source >>= 7;
        if (source < 1ul << 7) {
            span[offset] = (byte)source;
            return offset + 1;
        }

        span[offset++] = (byte)(HighBit | source);
        source >>= 7;
        if (source < 1ul << 7) {
            span[offset] = (byte)source;
            return offset + 1;
        }

        while (source >= HighBit) {
            span[offset++] = (byte)(HighBit | source);
            source >>= 7;
        }
        span[offset++] = (byte)source;
        return offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteFixedLVarInt32(this Span<byte> span, int value)
    {
        // Fixed-width (non-minimal) unsigned LEB128 encoding
        var v = (uint)value;
        var offset = FixedLVarInt32Size - 1;
        span[offset--] = (byte)((v >> 28) & 0x7Fu);
        span[offset--] = (byte)(((v >> 21) & 0x7Fu) | 0x80u);
        span[offset--] = (byte)(((v >> 14) & 0x7Fu) | 0x80u);
        span[offset--] = (byte)(((v >> 7) & 0x7Fu) | 0x80u);
        span[offset] = (byte)((v & 0x7Fu) | 0x80u);
    }

    public static (uint Value, int Offset) ReadVarUInt32(this ReadOnlySpan<byte> span, int offset = 0)
    {
#if NETCOREAPP3_1_OR_GREATER
        if (BitConverter.IsLittleEndian) {
            var first = span[offset];
            if (first <= LowBits)
                return (first, offset + 1);

            if (span.Length - offset >= 4) {
                var data = span.ReadUnchecked<uint>(offset);
                var stopBits = ~data & 0x00808080;
                if (stopBits != 0) {
                    var length = (BitOperations.TrailingZeroCount(stopBits) >> 3) + 1;
                    var fastValue = (data & 0x7F)
                        | ((data & 0x7F00) >> 1)
                        | ((data & 0x7F0000) >> 2);
                    fastValue &= (1u << (length * 7)) - 1;
                    return (fastValue, offset + length);
                }
            }
        }
#endif

        byte b;
        var value = 0u;
        var shift = 0;
        for (; shift < 28; shift += 7) {
            b = span[offset++];
            value |= (uint)(b & LowBits) << shift;
            if (b <= LowBits)
                goto exit;
        }

        // Read the 10th byte. Since we already read 63 bits (7 * 9),
        // the value of this byte must fit within 1 bit (64 - 63),
        // and it must not have the high bit set.
        b = span[offset++];
        if (b > 15)
            throw Errors.InvalidVarLengthEncodedValue();

        value |= (uint)b << shift;
        exit:
        return (value, offset);
    }

    public static (ulong Value, int Offset) ReadVarUInt64(this ReadOnlySpan<byte> span, int offset = 0)
    {
#if NETCOREAPP3_1_OR_GREATER
        if (BitConverter.IsLittleEndian) {
            var first = span[offset];
            if (first <= LowBits)
                return (first, offset + 1);

            var remainingLength = span.Length - offset;
            if (remainingLength >= 4) {
                var data = span.ReadUnchecked<uint>(offset);
                var stopBits = ~data & 0x00808080;
                if (stopBits != 0) {
                    var length = (BitOperations.TrailingZeroCount(stopBits) >> 3) + 1;
                    var fastValue = (data & 0x7F)
                        | ((data & 0x7F00) >> 1)
                        | ((data & 0x7F0000) >> 2);
                    fastValue &= (1u << (length * 7)) - 1;
                    return (fastValue, offset + length);
                }
            }

            if (remainingLength >= 10 && Bmi2.X64.IsSupported) {
                var data = span.ReadUnchecked<ulong>(offset);
                var tail = span.ReadUnchecked<ushort>(offset + 8);
                var stopBits = Bmi2.X64.ParallelBitExtract(~data, 0x8080808080808080)
                    | ((uint)(~tail & 0x80) << 1)
                    | ((uint)(~tail & 0x8000) >> 6);
                if (stopBits == 0)
                    throw Errors.InvalidVarLengthEncodedValue();

                var length = BitOperations.TrailingZeroCount(stopBits) + 1;
                if (length == 10 && (tail >> 8) > 1)
                    throw Errors.InvalidVarLengthEncodedValue();

                var fastValue = Bmi2.X64.ParallelBitExtract(data, 0x7F7F7F7F7F7F7F7F)
                    | ((ulong)(tail & 0x7F) << 56)
                    | ((ulong)(tail & 0x0100) << 55);
                if (length < 10)
                    fastValue &= (1ul << (length * 7)) - 1;
                return (fastValue, offset + length);
            }
        }
#endif

        byte b;
        var value = 0ul;
        var shift = 0;
        for (; shift < 63; shift += 7) {
            b = span[offset++];
            value |= (ulong)(b & LowBits) << shift;
            if (b <= LowBits)
                goto exit;
        }

        // Read the 10th byte. Since we already read 63 bits (7 * 9),
        // the value of this byte must fit within 1 bit (64 - 63),
        // and it must not have the high bit set.
        b = span[offset++];
        if (b > 1)
            throw Errors.InvalidVarLengthEncodedValue();

        value |= (ulong)b << shift;
        exit:
        return (value, offset);
    }
}
