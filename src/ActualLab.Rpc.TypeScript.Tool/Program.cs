using System.Runtime.Loader;
using System.Text;
using ActualLab.Fusion;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.TypeScript;

const string usage = """
    Usage: actuallab-tsgen <contracts.dll> --output <file.ts> [--service <Name>]... [--check]

    Generates TypeScript interfaces, DTOs and defineRpcService definitions for every public
    IRpcService / IComputeService interface declared in <contracts.dll>, except backend ones.

      -o, --output <file.ts>   The file to write.
      -s, --service <Name>     Generate only the listed services (short or full type name).
      --check                  Don't write anything; fail if <file.ts> is missing or out of date.
    """;

string? assemblyPath = null;
string? outputPath = null;
var serviceNames = new HashSet<string>(StringComparer.Ordinal);
var mustCheck = false;
for (var i = 0; i < args.Length; i++) {
    switch (args[i]) {
    case "-o" or "--output" when i + 1 < args.Length:
        outputPath = args[++i];
        break;
    case "-s" or "--service" when i + 1 < args.Length:
        serviceNames.Add(args[++i]);
        break;
    case "--check":
        mustCheck = true;
        break;
    case "-h" or "--help":
        WriteLine(usage);
        return 0;
    default:
        if (assemblyPath is not null || args[i].StartsWith('-')) {
            Error.WriteLine($"error: unexpected argument '{args[i]}'.\n\n{usage}");
            return 2;
        }

        assemblyPath = args[i];
        break;
    }
}
if (assemblyPath is null || outputPath is null) {
    Error.WriteLine(usage);
    return 2;
}

assemblyPath = Path.GetFullPath(assemblyPath);
outputPath = Path.GetFullPath(outputPath);
var assembly = LoadAssembly(assemblyPath);
var serviceTypes = assembly.GetExportedTypes()
    .Where(t => t is { IsInterface: true, IsGenericTypeDefinition: false }
        && typeof(IRpcService).IsAssignableFrom(t)
        && !typeof(IBackendService).IsAssignableFrom(t)
        && !typeof(IRpcSystemService).IsAssignableFrom(t))
    .Where(t => serviceNames.Count == 0 || serviceNames.Contains(t.Name) || serviceNames.Contains(t.FullName!))
    .OrderBy(t => t.FullName, StringComparer.Ordinal)
    .ToList();
var missingServiceNames = serviceNames
    .Where(n => !serviceTypes.Any(t => string.Equals(t.Name, n, StringComparison.Ordinal)
        || string.Equals(t.FullName, n, StringComparison.Ordinal)))
    .ToList();
if (missingServiceNames.Count != 0) {
    Error.WriteLine($"error: no such service(s) in {assembly.GetName().Name}: {string.Join(", ", missingServiceNames)}.");
    return 1;
}

if (serviceTypes.Count == 0)
    Error.WriteLine($"warning: {assembly.GetName().Name} declares no public RPC services.");

var services = new ServiceCollection();
var fusion = services.AddFusion();
foreach (var serviceType in serviceTypes) {
    if (typeof(IComputeService).IsAssignableFrom(serviceType))
        fusion.AddClient(serviceType);
    else
        fusion.Rpc.AddClient(serviceType);
}
var serviceProvider = services.BuildServiceProvider();
await using var _ = serviceProvider.ConfigureAwait(false);
var serviceRegistry = serviceProvider.RpcHub().ServiceRegistry;
var generator = new RpcTypeScriptGenerator(RpcTypeScriptGeneratorOptions.Default with {
    SourceName = assembly.GetName().Name ?? "",
});
var result = generator.Generate(serviceTypes.Select(t => serviceRegistry[t]));
foreach (var warning in result.Warnings)
    Error.WriteLine($"warning: {warning}");

var existingCode = File.Exists(outputPath) ? await File.ReadAllTextAsync(outputPath).ConfigureAwait(false) : null;
// Git may check the file out with CRLF line endings
if (string.Equals(existingCode?.ReplaceLineEndings("\n"), result.Code, StringComparison.Ordinal)) {
    WriteLine($"{outputPath} is up to date.");
    return 0;
}

if (mustCheck) {
    Error.WriteLine($"error: {outputPath} is out of date; rerun actuallab-tsgen without --check to regenerate it.");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
await File.WriteAllTextAsync(outputPath, result.Code, new UTF8Encoding(false)).ConfigureAwait(false);
WriteLine($"Generated {outputPath}.");
return 0;

static Assembly LoadAssembly(string path)
{
    // The tool's own ActualLab.* assemblies are reused (so IRpcService & co. are the same types),
    // the rest of the contracts' dependencies are resolved next to them or via their .deps.json
    var resolver = new AssemblyDependencyResolver(path);
    var directory = Path.GetDirectoryName(path)!;
    AssemblyLoadContext.Default.Resolving += (context, name) => {
        var dependencyPath = resolver.ResolveAssemblyToPath(name) ?? Path.Combine(directory, name.Name + ".dll");
        return File.Exists(dependencyPath) ? context.LoadFromAssemblyPath(dependencyPath) : null;
    };
    return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
}
