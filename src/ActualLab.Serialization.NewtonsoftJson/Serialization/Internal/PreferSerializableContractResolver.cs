using Newtonsoft.Json.Serialization;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A Newtonsoft.Json contract resolver that prefers <see cref="ISerializable"/> contracts when available.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2109", Justification = "Newtonsoft.Json is used by explicit opt-in")]
public class PreferSerializableContractResolver : DefaultContractResolver
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Newtonsoft.Json is used by explicit opt-in")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Newtonsoft.Json is used by explicit opt-in")]
    public PreferSerializableContractResolver()
    { }

    protected override JsonContract CreateContract(Type objectType)
    {
        if (typeof(ISerializable).IsAssignableFrom(objectType))
            return CreateISerializableContract(objectType);

        return base.CreateContract(objectType);
    }
}
