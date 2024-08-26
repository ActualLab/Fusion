using MemoryPack;

namespace Samples.MeshRpc.Services;

[MemoryPackable(GenerateType.VersionTolerant)]
[method: MemoryPackConstructor]
public sealed partial record CounterWithOrigin(
    [property: MemoryPackOrder(0)] Counter Counter,
    [property: MemoryPackOrder(1)] Symbol Origin)
{
    public override string ToString()
        => $"{Counter} @ {Origin}";
}
