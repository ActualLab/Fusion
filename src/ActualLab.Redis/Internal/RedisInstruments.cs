using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.Redis.Internal;

public static class RedisInstruments
{
    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
}
