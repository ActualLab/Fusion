using System.Runtime.Serialization;
using MemoryPack;
using MessagePack;

namespace Samples.MeshRpc;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public readonly partial record struct HostRef(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] string Id)
{
    public override string ToString()
        => $"host:{Id}";
}
