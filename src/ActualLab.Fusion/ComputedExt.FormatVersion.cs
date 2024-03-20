namespace ActualLab.Fusion;

public static partial class ComputedExt
{
    private static readonly string VersionDigits = MathExt.Digits32;

    public static string FormatVersion(this IComputed computed)
        => FormatVersion(computed.Version);
    public static string FormatVersion<T>(this Computed<T> computed)
        => FormatVersion(computed.Version);

    public static string FormatVersion(int version)
    {
        Span<char> buffer = stackalloc char[12]; // 8 is enough for Digits32
        buffer[0] = '@';
        var length = 1 + FormatBase32((uint)version, buffer[1..]);
#if !NETSTANDARD2_0
        return new string(buffer[..length]);
#else
        return buffer[..length].ToString();
#endif
    }

    // Private methods

    private static int FormatBase32(uint number, Span<char> buffer)
    {
        const uint mask = 31;
        const int shift = 5;
        if (number == 0) {
            buffer[0] = VersionDigits[0];
            return 1;
        }

        var index = buffer.Length;
        while (number != 0)  {
            var digit = (int)(number & mask);
            buffer[--index] = VersionDigits[digit];
            number >>= shift;
        }
        var tail = buffer[index..];
        tail.CopyTo(buffer);
        return tail.Length;
    }
}
