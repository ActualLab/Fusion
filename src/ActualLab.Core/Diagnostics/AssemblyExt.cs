using System.Diagnostics;
using ActualLab.OS;

namespace ActualLab.Diagnostics;

/// <summary>
/// Extension methods for <see cref="Assembly"/> to retrieve its informational version
/// and to tell whether it was built in Debug configuration.
/// </summary>
public static class AssemblyExt
{
    private static readonly ConcurrentDictionary<Assembly, string?> InformationalVersions
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<Assembly, bool> IsDebugBuildCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    // True when the assembly was compiled without optimizations, i.e. typically in Debug configuration
    public static bool IsDebugBuild(this Assembly assembly)
        => IsDebugBuildCache.GetOrAdd(assembly,
            static a => a.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled ?? false);

    public static string? GetInformationalVersion(this Assembly assembly)
        => InformationalVersionResolver.Invoke(assembly);

    // Overridable part

    public static Func<Assembly, string?> InformationalVersionResolver { get; set; } =
        assembly => InformationalVersions.GetOrAdd(assembly,
            static a => {
                var attrs = (AssemblyInformationalVersionAttribute[])a
                    .GetCustomAttributes(
                        typeof(AssemblyInformationalVersionAttribute),
                        inherit: false);
                var version = attrs.FirstOrDefault()?.InformationalVersion;
                if (!version.IsNullOrEmpty())
                    return version;

                if (OSInfo.IsWebAssembly)
                    return null;

                try {
                    version = FileVersionInfo.GetVersionInfo(a.Location).ProductVersion;
                    return version.NullIfEmpty();
                }
                catch {
                    return null;
                }
            });
}
