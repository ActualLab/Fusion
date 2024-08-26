using System.Runtime.Serialization;
using MemoryPack;

namespace Samples.MeshRpc;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public readonly partial record struct HostRef(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] Symbol Id)
{
    public override string ToString()
        => $"host:{Id.Value}";
}
