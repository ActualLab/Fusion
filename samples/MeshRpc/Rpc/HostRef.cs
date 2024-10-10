using System.Runtime.Serialization;
using MemoryPack;
using MessagePack;

namespace Samples.MeshRpc;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public readonly partial record struct HostRef(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] Symbol Id)
{
    public override string ToString()
        => $"host:{Id.Value}";
}
