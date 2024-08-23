using System.Globalization;

namespace ActualLab.Rpc.Infrastructure;

[StructLayout(LayoutKind.Sequential, Pack = 8)] // Important!
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public readonly partial record struct RpcObjectId(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] Guid HostId,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] long LocalId)
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public bool IsNone => LocalId == 0 && HostId == default;

    public override string ToString()
        => IsNone ? "" : $"{HostId}:{LocalId.ToString(CultureInfo.InvariantCulture)}";
}
