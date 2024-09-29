using ActualLab.Conversion;
using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public sealed partial record RpcMessageV1(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] byte CallTypeId,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] long RelatedId,
    [property: DataMember(Order = 2), MemoryPackOrder(2)] string Service,
    [property: DataMember(Order = 3), MemoryPackOrder(3)] string Method,
    [property: DataMember(Order = 4), MemoryPackOrder(4)] TextOrBytes ArgumentData,
    [property: DataMember(Order = 5), MemoryPackOrder(5)] RpcHeader[]? Headers
)
{
    public static readonly BiConverter<RpcMessage, RpcMessageV1> Converter = new(
        x => {
            var (service, method) = x.MethodRef.GetServiceAndMethodName();
            return new RpcMessageV1(x.CallTypeId, x.RelatedId, service, method, x.ArgumentData, x.Headers);
        },
        x => {
            var methodRef = new RpcMethodRef(x.Service, x.Method);
            return new RpcMessage(x.CallTypeId, x.RelatedId, methodRef, x.ArgumentData, x.Headers);
        });

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public ArgumentList? Arguments { get; init; }

    public override string ToString()
    {
        var headers = Headers.OrEmpty();
        return $"{nameof(RpcMessageV1)} #{RelatedId}/{CallTypeId}: {Service}.{Method}, "
            + (Arguments != null
                ? $"Arguments: {Arguments}"
                : $"ArgumentData: {ArgumentData.ToString(16)}")
            + (headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "");
    }

    // This record relies on referential equality
    public bool Equals(RpcMessageV1? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
