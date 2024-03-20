namespace ActualLab.Rpc.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial class RpcMessage
{
    [JsonInclude, DataMember(Order = 0), MemoryPackOrder(0)] public readonly byte CallTypeId;
    [JsonInclude, DataMember(Order = 1), MemoryPackOrder(1)] public readonly long RelatedId;
    [JsonInclude, DataMember(Order = 2), MemoryPackOrder(2)] public readonly string Service;
    [JsonInclude, DataMember(Order = 3), MemoryPackOrder(3)] public readonly string Method;
    [JsonInclude, DataMember(Order = 4), MemoryPackOrder(4)] public readonly TextOrBytes ArgumentData;
    [JsonInclude, DataMember(Order = 5), MemoryPackOrder(5)] public readonly List<RpcHeader>? Headers;

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcMessage(
        byte callTypeId,
        long relatedId,
        string service,
        string method,
        TextOrBytes argumentData,
        List<RpcHeader>? headers)
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
        return $"{nameof(RpcMessage)} #{RelatedId}/{CallTypeId}: {Service}.{Method}, "
            + $"ArgumentData: {ArgumentData.ToString(16)}"
            + (headers.Count > 0 ? $", Headers: {headers.ToDelimitedString()}" : "");
    }
}
