using ActualLab.OS;

namespace ActualLab.Fusion;

public static class ComputedVersion
{
    private static readonly long LocalVersionCount;

    private static readonly LocalVersion[] LocalVersions;
    [ThreadStatic] private static LocalVersion? _localVersion;

    static ComputedVersion()
    {
        var localVersionCount = HardwareInfo.GetProcessorCountFactor(4).Clamp(1, 1024);
        var primeSieve = FusionDefaults.GetPrimeSieve(localVersionCount + 16);
        while (!primeSieve.IsPrime(localVersionCount))
            localVersionCount--;
        LocalVersionCount = localVersionCount;
        LocalVersions = new LocalVersion[localVersionCount];
        for (var i = 0; i < LocalVersions.Length; i++)
            LocalVersions[i] = new LocalVersion(i);
    }

    public static ulong Next()
        => (_localVersion ??= LocalVersions[GetLocalVersionIndex()]).Next();

    // Private methods

    private static int GetLocalVersionIndex()
    {
        var m = Environment.CurrentManagedThreadId % LocalVersionCount;
        if (m < 0)
            m += LocalVersionCount;
        return (int)m;
    }

    // Nested types

    private sealed class LocalVersion
    {
        private long _version;

        // ReSharper disable once ConvertToPrimaryConstructor
        public LocalVersion(long initialVersion)
            => _version = initialVersion;

        // ReSharper disable once MemberHidesStaticFromOuterClass
        public ulong Next()
        {
            var result = Interlocked.Add(ref _version, LocalVersionCount);
            while (result == 0)
                result = Interlocked.Add(ref _version, LocalVersionCount);
            return (ulong)result;
        }
    }
}
