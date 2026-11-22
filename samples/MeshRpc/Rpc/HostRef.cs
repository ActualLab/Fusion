using System.Runtime.Serialization;
using MessagePack;

namespace Samples.MeshRpc;

[DataContract, MessagePackObject(true)]
public readonly partial record struct HostRef(
    [property: DataMember(Order = 0)] string Id)
{
    public override string ToString()
        => $"host:{Id}";
}
