using ActualLab.Interception;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public sealed class RpcOutboundMessage(
    RpcOutboundContext context,
    RpcMethodDef methodDef,
    long relatedId,
    bool needsPolymorphism,
    RpcHeader[]? headers,
    ReadOnlyMemory<byte> argumentData,
    RpcTransportSendHandler? sendHandler = null)
{
    public readonly RpcOutboundContext Context = context;
    public readonly RpcMethodDef MethodDef = methodDef;
    public readonly long RelatedId = relatedId;
    public readonly bool NeedsPolymorphism = needsPolymorphism;
    public readonly ReadOnlyMemory<byte> ArgumentData = argumentData;
    public readonly ArgumentList? Arguments = context.Arguments;
    public readonly RpcHeader[]? Headers = headers;
    public readonly RpcArgumentSerializer ArgumentSerializer = context.Peer!.ArgumentSerializer;
    public readonly RpcTransportSendHandler? SendHandler = sendHandler;

    public bool HasArguments => !ReferenceEquals(Arguments, null);
    public bool HasArgumentData => !ArgumentData.IsEmpty;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcOutboundMessage(
        RpcOutboundContext context,
        RpcMethodDef methodDef,
        long relatedId,
        bool needsPolymorphism,
        RpcHeader[]? headers,
        RpcTransportSendHandler? sendHandler = null)
        : this(context, methodDef, relatedId, needsPolymorphism, headers, default, sendHandler)
    { }

    public override string ToString()
    {
        var headers = Headers.OrEmpty();
        return $"{nameof(RpcOutboundMessage)} #{RelatedId}/{MethodDef.CallType.Id}: {MethodDef.Ref.FullName}, "
            + (HasArguments ? $"Arguments: {Arguments}, " : "")
            + (HasArgumentData ? $"ArgumentData: {new ByteString(ArgumentData).ToString(16)}, " : "")
            + (headers.Length > 0 ? $"Headers: {headers.ToDelimitedString()}" : "");
    }
}
