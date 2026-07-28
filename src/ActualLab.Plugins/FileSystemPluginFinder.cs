using System.Text.RegularExpressions;
#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.Loader;
#endif
using ActualLab.Caching;
using ActualLab.IO;
using ActualLab.Plugins.Internal;
using ActualLab.Plugins.Metadata;

namespace ActualLab.Plugins;

#pragma warning disable IL2026

/// <summary>
/// Discovers plugins by scanning assemblies in a file system directory for types marked with <see cref="PluginAttribute"/>.
/// </summary>
public class FileSystemPluginFinder : CachingPluginFinderBase
{
    /// <summary>
    /// Configuration options for <see cref="FileSystemPluginFinder"/>.
    /// </summary>
    public new record Options : CachingPluginFinderBase.Options
    {
        public FilePath PluginDir { get; init; } =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";

        public string AssemblyNamePattern { get; init; } = "*.dll";
        public Regex ExcludedAssemblyNamesRegex { get; init; } = new(
            @"((System)|(Microsoft)|(Google)|(WindowsBase)|(mscorlib))\.(.*)\.dll",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
        public bool UseCache { get; init; } = true;
        public bool DetectIndirectAssemblyDependencies { get; init; } = true;
        public FilePath CacheDir { get; init; } = FilePath.GetApplicationCacheDirectory();
    }

    public new Options Settings { get; }

    public FileSystemPluginFinder(
        Options settings,
        IPluginInfoProvider pluginInfoProvider,
        ILogger<FileSystemPluginFinder>? log = null)
        : base(settings, pluginInfoProvider, log ?? NullLogger<FileSystemPluginFinder>.Instance)
        // ReSharper disable once ConvertToPrimaryConstructor
        => Settings = settings;

    protected override IAsyncCache<string, string> CreateCache()
    {
        if (!Settings.UseCache) {
            Log.LogDebug("Cache isn't used");
            return new EmptyCache<string, string>();
        }

        var cacheDir = GetCacheDir();
        if (FilePath.IsWritableByOtherUsers(cacheDir)) {
            Log.LogWarning("Cache directory is writable by other users, so it isn't used: {CacheDirectory}", cacheDir);
            return new EmptyCache<string, string>();
        }

        var cache = new FileSystemCache<string, string>(cacheDir);
        Log.LogDebug("Cache directory: {CacheDirectory}", cache.CacheDirectory);
        return cache;
    }

    protected virtual FilePath GetCacheDir()
        => Settings.CacheDir;

    protected override string GetCacheKey()
    {
        var files = (
            from name in GetPluginAssemblyNames()
            let modifyDate = File.GetLastWriteTime(name)
            select (name, modifyDate.ToFileTime())
        ).ToArray();
        var detectIndirectDependencies = Settings.DetectIndirectAssemblyDependencies ? 1 : 0;
        // v2 = System.Text.Json; a v1 entry is Newtonsoft JSON, which reads back with silently
        // empty capabilities, so the version prefix must keep the two apart.
        return $"v2:{detectIndirectDependencies}:{files.ToDelimitedString()}";
    }

    protected override bool IsValidCachedPluginSet(PluginSetInfo pluginSetInfo)
    {
        // Accepts exactly what FindPlugins would have discovered - a public, non-abstract type
        // carrying an enabled PluginAttribute in one of the plugin directory's assemblies - so a
        // planted cache file can't widen the set of types that reach the plugin factory.
        var assemblyNames = new HashSet<string>(
            GetPluginAssemblyNames().Select(path => path.FileNameWithoutExtension.Value),
            StringComparer.OrdinalIgnoreCase);
        foreach (var typeRef in GetPluginTypeRefs(pluginSetInfo)) {
            if (!pluginSetInfo.InfoByType.ContainsKey(typeRef))
                return false;
            if (!IsDiscoverablePluginType(typeRef, assemblyNames))
                return false;
        }

        return true;
    }

    protected virtual FilePath[] GetPluginAssemblyNames()
        => Directory
            .EnumerateFiles(Settings.PluginDir, Settings.AssemblyNamePattern, SearchOption.TopDirectoryOnly)
            .Select(FilePath.New)
            .Where(path => !Settings.ExcludedAssemblyNamesRegex.IsMatch(path.Value))
            .OrderBy(path => path)
            .ToArray();

#pragma warning disable 1998
    protected override async Task<PluginSetInfo> FindPlugins(CancellationToken cancellationToken)
#pragma warning restore 1998
    {
        var plugins = new HashSet<Type>();
#if NETCOREAPP3_1_OR_GREATER
        var context = GetAssemblyLoadContext();
#endif
        foreach (var assemblyPath in GetPluginAssemblyNames()) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
#if !NETCOREAPP3_1_OR_GREATER
                var assembly = Assembly.LoadFile(assemblyPath);
#else
                var assembly = context.LoadFromAssemblyPath(assemblyPath);
#endif
                foreach (var type in GetExportedTypes(assembly, assemblyPath)) {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (type.IsAbstract || type.IsNotPublic)
                        continue;
                    var attr = type.GetCustomAttribute<PluginAttribute>();
                    if (attr?.IsEnabled == true)
                        plugins.Add(type);
                }
            }
            catch (Exception e) when (e is TypeLoadException or FileNotFoundException or FileLoadException) {
                Log.LogWarning(e, "Assembly load failed: {AssemblyName}", assemblyPath);
            }
        }

        return new PluginSetInfo(plugins,
            PluginInfoProvider,
            Settings.DetectIndirectAssemblyDependencies);
    }

    protected virtual IEnumerable<Type> GetExportedTypes(Assembly assembly, FilePath assemblyPath)
    {
        try {
            return assembly.ExportedTypes;
        }
        catch (ReflectionTypeLoadException e) {
            foreach (var loaderError in e.LoaderExceptions ?? Array.Empty<Exception>()) {
                if (loaderError is not null)
                    Log.LogWarning(loaderError, "Type load failed in assembly: {AssemblyName}", assemblyPath);
            }
            return e.Types.OfType<Type>();
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    protected virtual AssemblyLoadContext GetAssemblyLoadContext()
        => AssemblyLoadContext.Default;
#endif

    // Private methods

    private static IEnumerable<TypeRef> GetPluginTypeRefs(PluginSetInfo pluginSetInfo)
        => pluginSetInfo.InfoByType.Keys
            .Concat(pluginSetInfo.InfoByType.Values.Select(pluginInfo => pluginInfo.Type))
            .Concat(pluginSetInfo.TypesByBaseType.Values.SelectMany(typeRefs => typeRefs))
            .Concat(pluginSetInfo.TypesByBaseTypeOrderedByDependency.Values.SelectMany(typeRefs => typeRefs))
            .Distinct();

    private static bool IsDiscoverablePluginType(TypeRef typeRef, HashSet<string> assemblyNames)
    {
        // The name-based check runs first, so a cache naming a type outside the plugin directory
        // is rejected before Type.GetType gets a chance to load anything.
        var assemblyName = GetAssemblyName(typeRef);
        if (assemblyName is null || !assemblyNames.Contains(assemblyName))
            return false;

        var type = typeRef.TryResolve();
        if (type is null || type.IsAbstract || type.IsNotPublic)
            return false;
        if (!assemblyNames.Contains(type.Assembly.GetName().Name ?? ""))
            return false;

        return type.GetCustomAttribute<PluginAttribute>()?.IsEnabled == true;
    }

    private static string? GetAssemblyName(TypeRef typeRef)
    {
        // Generic arguments are nested in brackets and carry commas of their own, so only a
        // top-level comma separates the type name from the assembly name that follows it.
        var name = typeRef.AssemblyQualifiedName.AsSpan();
        var depth = 0;
        var start = -1;
        for (var i = 0; i < name.Length; i++) {
            var c = name[i];
            if (c == '[')
                depth++;
            else if (c == ']') {
                if (--depth < 0)
                    return null;
            }
            else if (c == ',' && depth == 0) {
                if (start >= 0)
                    return name.Slice(start, i - start).Trim().ToString();

                start = i + 1;
            }
        }
        return start >= 0 && start < name.Length
            ? name.Slice(start).Trim().ToString()
            : null;
    }
}
