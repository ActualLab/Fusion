using System.Diagnostics.Metrics;

namespace ActualLab.Diagnostics;

public static class MeterExt
{
    public static readonly Meter Unknown = new("unknown");
}
