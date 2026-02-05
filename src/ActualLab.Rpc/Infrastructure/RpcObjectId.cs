using System.Globalization;
using MessagePack;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Uniquely identifies a shared or remote RPC object by host ID and local ID.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)] // Important!
[DataContract, MemoryPackable, MessagePackObject]
public readonly partial record struct RpcObjectId(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] Guid HostId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] long LocalId)
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsNone => LocalId == 0 && HostId == default;

    public override string ToString()
        => IsNone ? "" : $"{HostId}:{LocalId.ToString(CultureInfo.InvariantCulture)}";
}
