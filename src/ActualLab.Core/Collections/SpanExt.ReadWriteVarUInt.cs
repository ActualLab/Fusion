using ActualLab.Internal;

namespace ActualLab.Collections;

public static partial class SpanExt
{
    private const byte LowBits = 0x7F;
    private const byte HighBit = 0x80;

    public static int WriteVarUInt32(this Span<byte> span, uint source, int offset = 0)
    {
        while (source >= HighBit) {
            span[offset++] = (byte)(HighBit | source);
            source >>= 7;
        }
        span[offset++] = (byte)source;
        return offset;
    }

    public static int WriteVarUInt64(this Span<byte> span, ulong source, int offset = 0)
    {
        while (source >= HighBit) {
            span[offset++] = (byte)(HighBit | source);
            source >>= 7;
        }
        span[offset++] = (byte)source;
        return offset;
    }

    public static (uint Value, int Offset) ReadVarUInt32(this ReadOnlySpan<byte> span, int offset = 0)
    {
        var value = 0u;

        // 1st byte (shift = 0)
        var b = span[offset++];
        value |= (uint)(b & LowBits);
        if (b <= LowBits) goto exit;

        // 2nd byte (shift = 7)
        b = span[offset++];
        value |= (uint)(b & LowBits) << 7;
        if (b <= LowBits) goto exit;

        // 3rd byte (shift = 14)
        b = span[offset++];
        value |= (uint)(b & LowBits) << 14;
        if (b <= LowBits) goto exit;

        // 4th byte (shift = 21)
        b = span[offset++];
        value |= (uint)(b & LowBits) << 21;
        if (b <= LowBits) goto exit;

        // 5th byte (final, shift = 28). Must be <= 0x0F and without high bit.
        b = span[offset++];
        if (b > 15)
            throw Errors.InvalidVarLengthEncoding<uint>();

        value |= (uint)b << 28;
    exit:
        return (value, offset);
    }

    public static (ulong Value, int Offset) ReadVarUInt64(this ReadOnlySpan<byte> span, int offset = 0)
    {
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
            throw Errors.InvalidVarLengthEncoding<ulong>();

        value |= (ulong)b << shift;
    exit:
        return (value, offset);
    }
}
