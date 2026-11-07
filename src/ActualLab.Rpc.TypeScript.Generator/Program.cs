using System.Text;
using ActualLab.Fusion;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.TypeScript;
using ActualLab.Rpc.TypeScript.Generator;

const string usage = """
    Usage: ActualLab.Rpc.TypeScript.Generator <contracts.dll> --output <file.ts> [--services <patterns>] [--check]

    Generates TypeScript interfaces, DTOs and defineRpcService definitions for every public
    IRpcService / IComputeService interface declared in <contracts.dll>, except backend ones.

      -o, --output <file.ts>     The file to write.
      -s, --services <patterns>  Comma-separated service type name patterns, where '*' matches
                                 any sequence of characters, e.g. "ITodo*,IStockApi". Default: "*".
      --check                    Don't write anything; fail if <file.ts> is missing or out of date.
    """;

string? assemblyPath = null;
string? outputPath = null;
var servicePatterns = new List<string>();
var mustCheck = false;
for (var i = 0; i < args.Length; i++) {
    switch (args[i]) {
    case "-o" or "--output" when i + 1 < args.Length:
        outputPath = args[++i];
        break;
    case "-s" or "--services" when i + 1 < args.Length:
        servicePatterns.Add(args[++i]);
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
if (!File.Exists(assemblyPath)) {
    Error.WriteLine($"error: {assemblyPath} doesn't exist.");
    return 1;
}

var assembly = LoadAssembly(assemblyPath);
var assemblyName = assembly.GetName().Name ?? "";
var filter = new RpcTypeScriptServiceFilter(string.Join(",", servicePatterns));
var candidateTypes = GetTypes(assembly)
    .Where(t => t is { IsPublic: true, IsInterface: true, IsGenericTypeDefinition: false }
        && typeof(IRpcService).IsAssignableFrom(t)
        && !typeof(IBackendService).IsAssignableFrom(t)
        && !typeof(IRpcSystemService).IsAssignableFrom(t))
    .ToList();
var unmatchedPatterns = filter.GetUnmatchedPatterns(candidateTypes).ToList();
var missingNames = unmatchedPatterns.Where(p => !p.Contains('*', StringComparison.Ordinal)).ToList();
if (missingNames.Count != 0) {
    Error.WriteLine($"error: no such service(s) in {assemblyName}: {string.Join(", ", missingNames)}.");
    return 1;
}

foreach (var pattern in unmatchedPatterns)
    Error.WriteLine($"warning: '{pattern}' matches no services in {assemblyName}.");
var serviceTypes = candidateTypes
    .Where(filter.IsMatch)
    .OrderBy(t => t.FullName, StringComparer.Ordinal)
    .ToList();
if (serviceTypes.Count == 0)
    Error.WriteLine($"warning: {assemblyName} declares no public RPC services matching '{filter}'.");

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
    SourceName = assemblyName,
});
var result = generator.Generate(serviceTypes.Select(t => serviceRegistry[t]));
foreach (var warning in result.Warnings)
    Error.WriteLine($"warning: {warning}");

var existingCode = File.Exists(outputPath)
    ? await File.ReadAllTextAsync(outputPath).ConfigureAwait(false)
    : null;
// Git may check the file out with CRLF line endings
if (string.Equals(existingCode?.ReplaceLineEndings("\n"), result.Code, StringComparison.Ordinal)) {
    WriteLine($"{outputPath} is up to date.");
    return 0;
}

if (mustCheck) {
    Error.WriteLine($"error: {outputPath} is out of date; rebuild without the check to regenerate it.");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
await File.WriteAllTextAsync(outputPath, result.Code, new UTF8Encoding(false)).ConfigureAwait(false);
WriteLine($"Generated {outputPath}.");
return 0;

static Assembly LoadAssembly(string path)
    => new ContractsLoadContext(path).LoadFromAssemblyPath(path);

static IEnumerable<Type> GetTypes(Assembly assembly)
{
    try {
        return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException e) {
        // Skips the types depending on assemblies that can't be loaded
        foreach (var message in e.LoaderExceptions.OfType<Exception>().Select(x => x.Message).Distinct(StringComparer.Ordinal))
            Error.WriteLine($"warning: {message}");
        return e.Types.OfType<Type>();
    }
}
