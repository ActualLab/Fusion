using ActualLab.Text;
using ActualLab.Time;
using MemoryPack;

namespace Samples.MeshRpc.Services;

[MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial record CounterState(
    [property: MemoryPackOrder(0)] Symbol HostId,
    [property: MemoryPackOrder(1)] Moment CreatedAt,
    [property: MemoryPackOrder(2)] int Value)
{
    public override string ToString()
        => $"{Value} @ {HostId}";
}
