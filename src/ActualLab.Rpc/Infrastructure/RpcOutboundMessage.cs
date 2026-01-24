using ActualLab.Interception;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcOutboundMessage(
    RpcOutboundContext context,
    RpcMethodDef methodDef,
    long relatedId,
    bool needsPolymorphism,
    RpcHeader[]? headers,
    ReadOnlyMemory<byte> argumentData = default)
{
    public readonly RpcOutboundContext Context = context;
    public readonly RpcMethodDef MethodDef = methodDef;
    public readonly long RelatedId = relatedId;
    public readonly bool NeedsPolymorphism = needsPolymorphism;
    public readonly ArgumentList Arguments = context.Arguments!;
    public readonly RpcHeader[]? Headers = headers;
    public readonly ReadOnlyMemory<byte> ArgumentData = argumentData;
    public readonly RpcArgumentSerializer ArgumentSerializer = context.Peer!.ArgumentSerializer;

    public bool HasArgumentData => !ArgumentData.IsEmpty;

    public override string ToString()
    {
        var headers = Headers.OrEmpty();
        return $"{nameof(RpcOutboundMessage)} #{RelatedId}/{MethodDef.CallType.Id}: {MethodDef.Ref.FullName}, "
            + (Arguments is not null ? $"Arguments: {Arguments}, " : "")
            + (HasArgumentData ? $"ArgumentData: {new ByteString(ArgumentData).ToString(16)}, " : "")
            + (headers.Length > 0 ? $"Headers: {headers.ToDelimitedString()}" : "");
    }
}
