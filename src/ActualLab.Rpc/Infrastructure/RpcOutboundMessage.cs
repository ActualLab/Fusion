using ActualLab.Interception;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcOutboundMessage
{
    private AsyncTaskMethodBuilder _whenSerializedBuilder;

    public readonly RpcOutboundContext Context;
    public readonly RpcMethodDef MethodDef;
    public readonly long RelatedId;
    public readonly bool NeedsPolymorphism;
    public readonly ArgumentList? Arguments;
    public readonly RpcHeader[]? Headers;
    public readonly ReadOnlyMemory<byte> ArgumentData;
    public readonly RpcArgumentSerializer ArgumentSerializer;
    public readonly Task? WhenSerialized;

    public bool HasArguments => !ReferenceEquals(Arguments, null);
    public bool HasArgumentData => !ArgumentData.IsEmpty;

    public RpcOutboundMessage(
        RpcOutboundContext context,
        RpcMethodDef methodDef,
        long relatedId,
        bool needsPolymorphism,
        bool tracksSerialization,
        RpcHeader[]? headers,
        ReadOnlyMemory<byte> argumentData = default)
    {
        Context = context;
        MethodDef = methodDef;
        RelatedId = relatedId;
        NeedsPolymorphism = needsPolymorphism;
        Arguments = context.Arguments;
        Headers = headers;
        ArgumentData = argumentData;
        ArgumentSerializer = context.Peer!.ArgumentSerializer;
        if (tracksSerialization) {
            _whenSerializedBuilder = AsyncTaskMethodBuilderExt.New();
            WhenSerialized = _whenSerializedBuilder.Task;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompleteWhenSerialized(Exception? error = null)
    {
        if (ReferenceEquals(_whenSerializedBuilder.Task, null))
            return;

        if (error is null)
            _whenSerializedBuilder.SetResult();
        else
            _whenSerializedBuilder.SetException(error);
    }

    public override string ToString()
    {
        var headers = Headers.OrEmpty();
        return $"{nameof(RpcOutboundMessage)} #{RelatedId}/{MethodDef.CallType.Id}: {MethodDef.Ref.FullName}, "
            + (HasArguments ? $"Arguments: {Arguments}, " : "")
            + (HasArgumentData ? $"ArgumentData: {new ByteString(ArgumentData).ToString(16)}, " : "")
            + (headers.Length > 0 ? $"Headers: {headers.ToDelimitedString()}" : "");
    }
}
