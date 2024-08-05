using ActualLab.Internal;

namespace ActualLab.Mathematics;

public static partial class Bits
{
    private const ulong Mask = 0x7Ful;
    private const ulong InvMask = 0xFFFF_FFFF_FFFF_FF80ul;
    private const int MaxBytesWithoutOverflow = 9;

    public static int Write7BitEncoded(ulong source, in Span<byte> target)
    {
        var index = 0;
        while (source >= 0x80ul) {
            target[index++] = (byte)(InvMask | source);
            source >>= 7;
        }
        target[index++] = (byte)source;
        return index;
    }

    public static int Read7BitEncoded(in Span<byte> source, out ulong result)
    {
        result = 0ul;
        byte b;
        for (int index = 0, shift = 0; index < MaxBytesWithoutOverflow; index++, shift += 7) {
            // ReadByte handles end of stream cases for us.
            b = source[index];
            result |= (b & Mask) << shift;
            if (b < 0x80u)
                return index + 1;
        }

        // Read the 10th byte. Since we already read 63 bits (7 * 9),
        // the value of this byte must fit within 1 bit (64 - 63),
        // and it must not have the high bit set.
        b = source[MaxBytesWithoutOverflow];
        if (b > 1u)
            throw Errors.Invalid7BitEncoded<ulong>();

        result |= (ulong)b << (MaxBytesWithoutOverflow * 7);
        return MaxBytesWithoutOverflow + 1;
    }
}
