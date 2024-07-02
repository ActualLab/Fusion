using System.Diagnostics;

namespace ActualLab.Time;

public sealed class CpuClock : MomentClock
{
    internal static readonly DateTime Zero = DateTime.UtcNow;
    internal static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

    public static readonly CpuClock Instance = new();

    public override Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Zero + Stopwatch.Elapsed);
    }

    private CpuClock() { }
}
