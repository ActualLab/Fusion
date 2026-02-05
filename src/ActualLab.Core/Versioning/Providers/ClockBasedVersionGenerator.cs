namespace ActualLab.Versioning.Providers;

/// <summary>
/// A <see cref="VersionGenerator{TVersion}"/> that generates monotonically increasing
/// <see cref="long"/> versions based on <see cref="MomentClock"/> ticks.
/// </summary>
public sealed class ClockBasedVersionGenerator(MomentClock clock) : VersionGenerator<long>
{
    public static VersionGenerator<long> Default { get; set; } = new ClockBasedVersionGenerator(MomentClockSet.Default.SystemClock);

#pragma warning disable MA0061
    public override long NextVersion(long currentVersion = 0)
#pragma warning restore MA0061
    {
        var nextVersion = clock.Now.EpochOffset.Ticks;
        return nextVersion > currentVersion ? nextVersion : ++currentVersion;
    }
}
