using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.Interception.Internal;

public static class InterceptionInstruments
{
    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
}
