namespace ActualLab.OS;

public static class RuntimeInfo
{
    public static class Process
    {
        public static readonly Guid Guid = Guid.NewGuid();
        public static readonly string Id = Guid.Format(62);
        public static readonly string MachinePrefixedId = $"{Environment.MachineName}-{Id}";
    }

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
