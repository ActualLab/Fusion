using System.Diagnostics;
using ActualLab.OS;

namespace ActualLab.Diagnostics;

/// <summary>
/// Extension methods for <see cref="Assembly"/> to retrieve informational version.
/// </summary>
public static class AssemblyExt
{
    private static readonly ConcurrentDictionary<Assembly, string?> InformationalVersions
        = new(HardwareInfo.ProcessorCountPo2, 131);

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
