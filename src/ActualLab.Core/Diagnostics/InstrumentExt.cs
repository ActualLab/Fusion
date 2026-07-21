using System.Diagnostics.Metrics;

namespace ActualLab.Diagnostics;

public static class InstrumentExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? IfEnabled<T>(this T instrument)
        where T : Instrument
        => instrument.Enabled ? instrument : null;
}
