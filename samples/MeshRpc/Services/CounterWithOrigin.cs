using MemoryPack;
using MessagePack;

namespace Samples.MeshRpc.Services;

[MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[method: MemoryPackConstructor, SerializationConstructor]
public sealed partial record CounterWithOrigin(
    [property: MemoryPackOrder(0), Key(0)] Counter Counter,
    [property: MemoryPackOrder(1), Key(1)] Symbol Origin)
{
    public override string ToString()
        => $"{Counter} @ {Origin}";
}
