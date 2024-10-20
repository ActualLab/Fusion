using ActualLab.OS;

namespace ActualLab.Fusion;

public static class ComputedVersion
{
    private static readonly int LocalVersionCount;
    private static readonly int LocalVersionCountMask;

    private static readonly LocalVersion[] LocalVersions;
    [ThreadStatic] private static LocalVersion? _localVersion;

    static ComputedVersion()
    {
        LocalVersionCount = HardwareInfo.GetProcessorCountPo2Factor(4).Clamp(1, 1024);
        LocalVersionCountMask = LocalVersionCount - 1;
        LocalVersions = new LocalVersion[LocalVersionCount];
        for (var i = 0; i < LocalVersions.Length; i++)
            LocalVersions[i] = new LocalVersion(i);
    }

    public static ulong Next()
    {
        var localVersion = _localVersion ??= LocalVersions[Environment.CurrentManagedThreadId & LocalVersionCountMask];
        while (true) {
            var result = Interlocked.Add(ref localVersion.Value, LocalVersionCount);
            if (result != 0)
                return (ulong)result;
        }
    }

    // Nested types

    private sealed class LocalVersion(long value)
    {
        public long Value = value;
    }
}
