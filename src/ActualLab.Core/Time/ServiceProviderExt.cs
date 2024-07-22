namespace ActualLab.Time;

public static class ServiceProviderExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MomentClockSet Clocks(this IServiceProvider services)
    {
        var clocks = services.GetService<MomentClockSet>();
        return clocks ?? MomentClockSet.Default;
    }
}
