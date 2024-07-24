using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActualLab.Diagnostics;

public static class DiagnosticsExt
{
    private static readonly ConcurrentDictionary<Assembly, ActivitySource> ActivitySources = new();
    private static readonly ConcurrentDictionary<Assembly, Meter> Meters = new();
    private const string UnknownName = "unknown";
    private const string UnknownVersion = "v_unknown";

    // Resolvers

    public static Func<Type, ActivitySource> TypeActivitySourceResolver { get; set; } =
        type => type.Assembly.GetActivitySource();

    public static Func<Assembly, ActivitySource> AssemblyActivitySourceResolver { get; set; } =
        assembly => ActivitySources.GetOrAdd(assembly,
            static a => new ActivitySource(
                a.GetName().Name ?? UnknownName,
                a.GetInformationalVersion() ?? UnknownVersion));

    public static Func<Type, Meter> TypeMeterResolver { get; set; } =
        type => type.Assembly.GetMeter();

    public static Func<Assembly, Meter> AssemblyMeterResolver { get; set; } =
        assembly => Meters.GetOrAdd(assembly,
            static a => new Meter(
                a.GetName().Name ?? UnknownName,
                a.GetInformationalVersion() ?? UnknownVersion));

    // GetActivitySource

    public static ActivitySource GetActivitySource(this Type type)
        => TypeActivitySourceResolver.Invoke(type);

    public static ActivitySource GetActivitySource(this Assembly assembly)
        => AssemblyActivitySourceResolver.Invoke(assembly);

    // GetMeter

    public static Meter GetMeter(this Type type)
        => TypeMeterResolver.Invoke(type);

    public static Meter GetMeter(this Assembly assembly)
        => AssemblyMeterResolver.Invoke(assembly);
}
