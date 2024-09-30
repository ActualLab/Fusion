using ActualLab.Conversion;
using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial class RpcMessageV1
{
    // Static members

    public static readonly BiConverter<RpcMessage, RpcMessageV1> Converter = new(New, ToRpcMessage);

    public static RpcMessageV1 New(RpcMessage x)
    {
        var (service, method) = x.MethodRef.GetServiceAndMethodName();
        return new RpcMessageV1(x.CallTypeId, x.RelatedId, service, method, x.ArgumentData, x.Headers);
    }

    // Instance members

    [DataMember(Order = 0), MemoryPackOrder(0)] public byte CallTypeId { get; init; }
    [DataMember(Order = 1), MemoryPackOrder(1)] public long RelatedId { get; init; }
    [DataMember(Order = 2), MemoryPackOrder(2)] public string Service { get; init; }
    [DataMember(Order = 3), MemoryPackOrder(3)] public string Method { get; init; }
    [DataMember(Order = 4), MemoryPackOrder(4)] public TextOrBytes ArgumentData { get; init; }
    [DataMember(Order = 5), MemoryPackOrder(5)] public RpcHeader[]? Headers { get; init; }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public ArgumentList? Arguments { get; init; }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcMessageV1(
        byte callTypeId, long relatedId,
        string service, string method, TextOrBytes argumentData,
        RpcHeader[]? headers)
    {
        CallTypeId = callTypeId;
        RelatedId = relatedId;
        Service = service;
        Method = method;
        ArgumentData = argumentData;
        Headers = headers;
    }

    public override string ToString()
    {
        var headers = Headers.OrEmpty();
        return $"{nameof(RpcMessageV1)} #{RelatedId}/{CallTypeId}: {Service}.{Method}, "
            + (Arguments != null
                ? $"Arguments: {Arguments}"
                : $"ArgumentData: {ArgumentData.ToString(16)}")
            + (headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "");
    }

    public static RpcMessage ToRpcMessage(RpcMessageV1 x)
    {
        var methodRef = new RpcMethodRef(x.Service, x.Method);
        return new RpcMessage(x.CallTypeId, x.RelatedId, methodRef, x.ArgumentData, x.Headers);
    }

    // This record relies on referential equality
    public bool Equals(RpcMessageV1? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public void Deconstruct(out byte CallTypeId, out long RelatedId, out string Service, out string Method, out TextOrBytes ArgumentData, out RpcHeader[]? Headers)
    {
        CallTypeId = this.CallTypeId;
        RelatedId = this.RelatedId;
        Service = this.Service;
        Method = this.Method;
        ArgumentData = this.ArgumentData;
        Headers = this.Headers;
    }
}
