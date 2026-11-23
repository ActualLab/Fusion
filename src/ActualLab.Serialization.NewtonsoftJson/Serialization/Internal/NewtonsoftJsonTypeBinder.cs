using ActualLab.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// The default Newtonsoft.Json serialization binder that also resolves the "$type" names of types moved
/// out of ActualLab.Core in v16 (see <see cref="TypeRef.Resolve(string)"/>), so older JSON stays readable.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2109", Justification = "Newtonsoft.Json is used by explicit opt-in")]
public class NewtonsoftJsonTypeBinder : DefaultSerializationBinder
{
    public static readonly NewtonsoftJsonTypeBinder Instance = new();

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Newtonsoft.Json is used by explicit opt-in")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Newtonsoft.Json is used by explicit opt-in")]
    public NewtonsoftJsonTypeBinder()
    { }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume serializable types are fully preserved")]
    public override Type BindToType(string? assemblyName, string typeName)
    {
        Type? type = null;
        try {
            return base.BindToType(assemblyName, typeName);
        }
        catch (JsonSerializationException) when ((type = ResolveMovedType(assemblyName, typeName)) is not null) {
            return type;
        }
    }

    // Private methods

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    private static Type? ResolveMovedType(string? assemblyName, string typeName)
        => TypeRef.Resolve(assemblyName is null ? typeName : $"{typeName}, {assemblyName}");
}
