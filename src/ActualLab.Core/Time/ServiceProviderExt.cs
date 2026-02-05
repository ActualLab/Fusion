namespace ActualLab.Time;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to resolve <see cref="MomentClockSet"/>.
/// </summary>
public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MomentClockSet Clocks(this IServiceProvider services)
    {
        var clocks = services.GetService<MomentClockSet>();
        return clocks ?? MomentClockSet.Default;
    }
}
