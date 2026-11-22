using Newtonsoft.Json.Serialization;

namespace ActualLab.Serialization.Internal;

#pragma warning disable IL2026, IL2109

/// <summary>
/// A Newtonsoft.Json contract resolver that prefers <see cref="ISerializable"/> contracts when available.
/// </summary>
public class PreferSerializableContractResolver : DefaultContractResolver
{
    protected override JsonContract CreateContract(Type objectType)
    {
        if (typeof(ISerializable).IsAssignableFrom(objectType))
            return CreateISerializableContract(objectType);

        return base.CreateContract(objectType);
    }
}
