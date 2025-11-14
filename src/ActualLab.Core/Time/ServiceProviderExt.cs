namespace ActualLab.Time;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MomentClockSet Clocks()
        {
            var clocks = services.GetService<MomentClockSet>();
            return clocks ?? MomentClockSet.Default;
        }
    }
}
