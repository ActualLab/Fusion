namespace ActualLab.Versioning;

public static class LongExt
{
    private static readonly string FormatVersionDigits = MathExt.Digits32;

    public static string FormatVersion(this long version)
        => ((ulong)version).FormatVersion();

    public static string FormatVersion(this long version, char prefix)
        => ((ulong)version).FormatVersion(prefix);

    public static string FormatVersion(this ulong version)
    {
        Span<char> buffer = stackalloc char[16]; // 14 is enough for Digits32
        var length = FormatVersion(version, buffer);
#if !NETSTANDARD2_0
        return new string(buffer[..length]);
#else
        return buffer[..length].ToString();
#endif
    }

    public static string FormatVersion(this ulong version, char prefix)
    {
        Span<char> buffer = stackalloc char[16]; // 14 is enough for Digits32
        buffer[0] = prefix;
        var length = 1 + FormatVersion(version, buffer[1..]);
#if !NETSTANDARD2_0
        return new string(buffer[..length]);
#else
        return buffer[..length].ToString();
#endif
    }

    // Private methods

    private static int FormatVersion(ulong n, Span<char> buffer)
    {
        const ulong mask = 31;
        const int shift = 5;
        if (n == 0) {
            buffer[0] = FormatVersionDigits[0];
            return 1;
        }

        var index = buffer.Length;
        while (n != 0)  {
            var digit = (int)(n & mask);
            buffer[--index] = FormatVersionDigits[digit];
            n >>= shift;
        }
        var tail = buffer[index..];
        tail.CopyTo(buffer);
        return tail.Length;
    }
}
