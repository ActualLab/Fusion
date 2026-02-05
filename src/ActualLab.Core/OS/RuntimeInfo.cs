namespace ActualLab.OS;

/// <summary>
/// Provides runtime environment information such as server/client mode
/// and process identity.
/// </summary>
public static class RuntimeInfo
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif

    /// <summary>
    /// Allows optimizing for server or client, typically for performance reasons.
    /// This property must be changed as early as possible on startup,
    /// otherwise its default value might be already used.
    /// </summary>
    public static bool IsServer {
        get;
        set {
            lock (StaticLock)
                field = value;
        }
    } = !OSInfo.IsAnyClient;

    /// <summary>
    /// Provides unique identifiers for the current process instance.
    /// </summary>
    public static class Process
    {
        public static readonly Guid Guid = Guid.NewGuid();
        public static readonly string Id = Guid.Format(62);
        public static readonly string MachinePrefixedId = $"{Environment.MachineName}-{Id}";
    }

    /// <summary>
    /// Provides .NET runtime version information.
    /// </summary>
    public static class DotNet
    {
        public static readonly string? VersionString;
        public static readonly Version? Version;

        static DotNet()
        {
            var assembly = typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly;
            var assemblyPath = assembly.Location.Split(
                new[] {'/', '\\'}, StringSplitOptions.RemoveEmptyEntries);
            var netCoreAppIndex = Array.IndexOf(assemblyPath, "Microsoft.NETCore.App");
            if (netCoreAppIndex > 0 && netCoreAppIndex < assemblyPath.Length - 2) {
                VersionString = assemblyPath[netCoreAppIndex + 1];
                if (Version.TryParse(VersionString.NullIfEmpty() ?? "", out var version))
                    Version = version;
            }
        }
    }
}
