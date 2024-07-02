using ActualLab.Text;
using MemoryPack;

namespace Samples.MeshRpc.Services;

[MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial record CounterState(
    [property: MemoryPackOrder(0)] Symbol HostId,
    [property: MemoryPackOrder(1)] int Value)
{
    public override string ToString()
        => $"{Value} from {HostId}";
}
