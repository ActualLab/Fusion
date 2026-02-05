using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.Redis.Internal;

/// <summary>
/// Provides shared OpenTelemetry instrumentation instances (ActivitySource and Meter)
/// for the Redis library.
/// </summary>
public static class RedisInstruments
{
    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
}
