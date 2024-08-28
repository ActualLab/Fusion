using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization.Internal;

[DataContract, MemoryPackable(SerializeLayout.Sequential)] // Not GenerateType.VersionTolerant!
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public readonly partial struct FastRpcMessage
{
    [DataMember(Order = 0)] public readonly byte CallTypeId;
    [DataMember(Order = 1)] public readonly long RelatedId;
#if !NETSTANDARD2_0
    [Utf16StringFormatter]
#endif
    [DataMember(Order = 2)] public readonly string Service;
#if !NETSTANDARD2_0
    [Utf16StringFormatter]
#endif
    [DataMember(Order = 3)] public readonly string Method;
    [DataMember(Order = 4)] public readonly RpcHeader[]? Headers;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FastRpcMessage(RpcMessage source)
    {
        CallTypeId = source.CallTypeId;
        RelatedId = source.RelatedId;
        Service = source.Service;
        Method = source.Method;
        Headers = source.Headers;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcMessage ToRpcMessage(TextOrBytes argumentData)
        => new(CallTypeId, RelatedId, Service, Method, argumentData, Headers);
}
