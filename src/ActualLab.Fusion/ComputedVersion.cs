using ActualLab.OS;

namespace ActualLab.Fusion;

public static class ComputedVersion
{
    private static readonly long LocalVersionCount;
    private static readonly string FormatDigits = MathExt.Digits32;

    private static readonly LocalVersion[] LocalVersions;
    [ThreadStatic] private static LocalVersion? _localVersion;

    static ComputedVersion()
    {
        var localVersionCount = HardwareInfo.GetProcessorCountFactor(4).Clamp(1, 1024);
        var primeSieve = FusionSettings.GetPrimeSieve(localVersionCount + 16);
        while (!primeSieve.IsPrime(localVersionCount))
            localVersionCount--;
        LocalVersionCount = localVersionCount;
        LocalVersions = new LocalVersion[localVersionCount];
        for (var i = 0; i < LocalVersions.Length; i++)
            LocalVersions[i] = new LocalVersion(i);
    }

    public static ulong Next()
        => (_localVersion ??= LocalVersions[GetLocalVersionIndex()]).Next();

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

    private static int GetLocalVersionIndex()
    {
        var m = Environment.CurrentManagedThreadId % LocalVersionCount;
        if (m < 0)
            m += LocalVersionCount;
        return (int)m;
    }

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

    // Nested types

    private class LocalVersion
    {
        private long _version;

        // ReSharper disable once ConvertToPrimaryConstructor
        public LocalVersion(long initialVersion)
            => _version = initialVersion;

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public ulong Next()
        {
            var result = Interlocked.Add(ref _version, LocalVersionCount);
            if (result == 0)
                result = Interlocked.Add(ref _version, LocalVersionCount);
            return (ulong)result;
        }
    }
}
