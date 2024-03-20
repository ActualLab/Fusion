namespace ActualLab.Fusion;

public static class ComputedVersion
{
    private const ulong Increment = 100_003; // Prime
    private static readonly string FormatDigits = MathExt.Digits32;

    private static readonly object Lock = new();
    private static ulong _lastThreadId;
    [ThreadStatic] private static ulong _threadId;
    [ThreadStatic] private static ulong _lastLocal;

    public static ulong Next()
    {
        // Double-check locking
        if (_lastLocal != 0) return _lastLocal += Increment;
        lock (Lock) {
            if (_lastLocal != 0) return _lastLocal += Increment;

            if (_threadId == 0)
                _threadId = ++_lastThreadId;
            _lastLocal = _threadId;
            return _lastLocal;
        }
    }

    public static string Format(ulong version)
    {
        Span<char> buffer = stackalloc char[16]; // 14 is enough for Digits32
        var length = FormatBase32(version, buffer);
#if !NETSTANDARD2_0
        return new string(buffer[..length]);
#else
        return buffer[..length].ToString();
#endif
    }

    public static string Format(char prefix, ulong version)
    {
        Span<char> buffer = stackalloc char[16]; // 14 is enough for Digits32
        buffer[0] = prefix;
        var length = 1 + FormatBase32(version, buffer[1..]);
#if !NETSTANDARD2_0
        return new string(buffer[..length]);
#else
        return buffer[..length].ToString();
#endif
    }

    // Private methods

    private static int FormatBase32(ulong n, Span<char> buffer)
    {
        const ulong mask = 31;
        const int shift = 5;
        if (n == 0) {
            buffer[0] = FormatDigits[0];
            return 1;
        }

        var index = buffer.Length;
        while (n != 0)  {
            var digit = (int)(n & mask);
            buffer[--index] = FormatDigits[digit];
            n >>= shift;
        }
        var tail = buffer[index..];
        tail.CopyTo(buffer);
        return tail.Length;
    }
}
