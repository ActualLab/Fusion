namespace ActualLab.Versioning;

/// <summary>
/// A <see cref="VersionGenerator{TVersion}"/> that generates monotonically increasing
/// <see cref="long"/> versions based on <see cref="CpuTimestamp"/> ticks.
/// </summary>
public sealed class CpuTimestampBasedVersionGenerator : VersionGenerator<long>
{
    public static VersionGenerator<long> Instance { get; set; } = new CpuTimestampBasedVersionGenerator();

#pragma warning disable MA0061
    public override long NextVersion(long currentVersion = 0)
#pragma warning restore MA0061
    {
        var nextVersion = CpuTimestamp.Now.Value & long.MaxValue;
        return nextVersion > currentVersion ? nextVersion : ++currentVersion;
    }
}
