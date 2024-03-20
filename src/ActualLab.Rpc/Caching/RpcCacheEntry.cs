namespace ActualLab.Rpc.Caching;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public sealed partial record RpcCacheEntry(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] RpcCacheKey Key,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] TextOrBytes Data
) {
    public override string ToString()
        => $"{nameof(RpcCacheEntry)}({Key} -> {Data.ToString(16)})";

    // This record relies on referential equality
    public bool Equals(RpcCacheEntry? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
