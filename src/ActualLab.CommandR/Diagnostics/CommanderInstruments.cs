using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.CommandR.Diagnostics;

public static class CommanderInstruments
{
    public static readonly ActivitySource ActivitySource = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
    public static readonly Meter Meter = new(ThisAssembly.AssemblyName, ThisAssembly.AssemblyVersion);
}
