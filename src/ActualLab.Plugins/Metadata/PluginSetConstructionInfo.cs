namespace ActualLab.Plugins.Metadata;

/// <summary>
/// Holds intermediate data used during <see cref="PluginSetInfo"/> construction,
/// including discovered plugin types and assembly dependency graphs.
/// </summary>
public class PluginSetConstructionInfo
{
    public Type[] Plugins { get; set; } = null!;
    public Assembly[] Assemblies { get; set; } = null!;
    public Dictionary<Assembly, HashSet<Assembly>> AssemblyDependencies { get; set; } = null!;
}
