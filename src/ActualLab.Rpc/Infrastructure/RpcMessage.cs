namespace ActualLab.Rpc.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public sealed partial record RpcMessage(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] byte CallTypeId,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] long RelatedId,
    [property: DataMember(Order = 2), MemoryPackOrder(2)] string Service,
    [property: DataMember(Order = 3), MemoryPackOrder(3)] string Method,
    [property: DataMember(Order = 4), MemoryPackOrder(4)] TextOrBytes ArgumentData,
    [property: DataMember(Order = 5), MemoryPackOrder(5)] List<RpcHeader>? Headers
) {
    public override string ToString()
    {
        var headers = Headers.OrEmpty();
        return $"{nameof(RpcMessage)} #{RelatedId}/{CallTypeId}: {Service}.{Method}, "
            + $"ArgumentData: {ArgumentData.ToString(16)}"
            + (headers.Count > 0 ? $", Headers: {headers.ToDelimitedString()}" : "");
    }

    // This record relies on referential equality
    public bool Equals(RpcMessage? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
