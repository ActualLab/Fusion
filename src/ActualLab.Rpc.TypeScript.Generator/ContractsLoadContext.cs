using System.Runtime.Loader;

namespace ActualLab.Rpc.TypeScript.Generator;

internal sealed class ContractsLoadContext(string contractsPath) : AssemblyLoadContext("Contracts")
{
    private readonly AssemblyDependencyResolver _resolver = new(contractsPath);
    private readonly string _directory = Path.GetDirectoryName(contractsPath)!;

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // The generator's own assemblies (ActualLab.* & co.) are reused whatever version the contracts
        // reference, so IRpcService & other types it checks are the same types. Matching by name only
        // lets it read contracts built against a newer ActualLab.* (e.g. from source), which the
        // default context refuses to bind to the older assemblies it already has.
        try {
            return Default.LoadFromAssemblyName(new AssemblyName(assemblyName.Name!));
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException) {
            // Not one of the generator's assemblies
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName)
            ?? Path.Combine(_directory, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}
