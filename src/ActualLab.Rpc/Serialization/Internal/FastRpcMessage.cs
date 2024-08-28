using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization.Internal;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial struct FastRpcMessage
{
    [DataMember(Order = 0), MemoryPackOrder(0)]
    public readonly byte CallTypeId;
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public readonly long RelatedId;
    [DataMember(Order = 2), MemoryPackOrder(2)]
#if !NETSTANDARD2_0
    [Utf16StringFormatter]
#endif
    public readonly string Service;
    [DataMember(Order = 3), MemoryPackOrder(3)]
#if !NETSTANDARD2_0
    [Utf16StringFormatter]
#endif
    public readonly string Method;
    [DataMember(Order = 5), MemoryPackOrder(5)]
    public readonly RpcHeader[]? Headers;

    public FastRpcMessage(RpcMessage source)
    {
        CallTypeId = source.CallTypeId;
        RelatedId = source.RelatedId;
        Service = source.Service;
        Method = source.Method;
        Headers = source.Headers;
    }

    [method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public FastRpcMessage(
        byte callTypeId,
        long relatedId,
        string service,
        string method,
        RpcHeader[]? headers)
    {
        CallTypeId = callTypeId;
        RelatedId = relatedId;
        Service = service;
        Method = method;
        Headers = headers;
    }

    public RpcMessage ToRpcMessage(TextOrBytes argumentData)
        => new(CallTypeId, RelatedId, Service, Method, argumentData, Headers);
}
