using ActualLab.Interception;
using MessagePack;

namespace ActualLab.Rpc.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public sealed partial record RpcMessage
{
    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)] public byte CallTypeId { get; init; }
    [DataMember(Order = 1), MemoryPackOrder(1), Key(1)] public long RelatedId { get; init; }
    [DataMember(Order = 2), MemoryPackOrder(2), Key(2)] public RpcMethodRef MethodRef { get; init; }
    [DataMember(Order = 3), MemoryPackOrder(3), Key(3)] public TextOrBytes ArgumentData { get; init; }
    [DataMember(Order = 4), MemoryPackOrder(4), Key(4)] public RpcHeader[]? Headers { get; init; }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public ArgumentList? Arguments { get; init; }

    [MemoryPackConstructor, SerializationConstructor]
    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcMessage(byte callTypeId, long relatedId, RpcMethodRef methodRef, TextOrBytes argumentData, RpcHeader[]? headers)
    {
        CallTypeId = callTypeId;
        RelatedId = relatedId;
        MethodRef = methodRef;
        ArgumentData = argumentData;
        Headers = headers;
    }

    public override string ToString()
    {
        var headers = Headers.OrEmpty();
        return $"{nameof(RpcMessageV1)} #{RelatedId}/{CallTypeId}: {MethodRef.GetFullMethodName()}, "
            + (Arguments != null
                ? $"Arguments: {Arguments}"
                : $"ArgumentData: {ArgumentData.ToString(16)}")
            + (headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "");
    }

    // This record relies on referential equality
    public bool Equals(RpcMessage? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
