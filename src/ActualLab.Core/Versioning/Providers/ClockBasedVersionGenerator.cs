namespace ActualLab.Versioning.Providers;

public sealed class ClockBasedVersionGenerator(IMomentClock clock) : VersionGenerator<long>
{
    public static VersionGenerator<long> DefaultPrecise { get; set; } = new ClockBasedVersionGenerator(MomentClockSet.Default.SystemClock);
    public static VersionGenerator<long> DefaultCoarse { get; set; } = new ClockBasedVersionGenerator(MomentClockSet.Default.CoarseSystemClock);

#pragma warning disable MA0061
    public override long NextVersion(long currentVersion = 0)
#pragma warning restore MA0061
    {
        var nextVersion = clock.Now.EpochOffset.Ticks;
        return nextVersion > currentVersion ? nextVersion : ++currentVersion;
    }
}
