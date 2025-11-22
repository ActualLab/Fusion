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
