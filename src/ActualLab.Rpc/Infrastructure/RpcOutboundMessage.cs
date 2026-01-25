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
    private AsyncTaskMethodBuilder _whenSerializedBuilder;

    public readonly RpcOutboundContext Context = context;
    public readonly RpcMethodDef MethodDef = methodDef;
    public readonly long RelatedId = relatedId;
    public readonly bool NeedsPolymorphism = needsPolymorphism;
    public readonly ArgumentList? Arguments = context.Arguments;
    public readonly RpcHeader[]? Headers = headers;
    public readonly ReadOnlyMemory<byte> ArgumentData = argumentData;
    public readonly RpcArgumentSerializer ArgumentSerializer = context.Peer!.ArgumentSerializer;

    public bool HasArguments => !ReferenceEquals(Arguments, null);
    public bool HasArgumentData => !ArgumentData.IsEmpty;

    // Used by lock-free transport to track serialization completion
    public Task WhenSerialized {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _whenSerializedBuilder.Task;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PrepareWhenSerialized()
        => _whenSerializedBuilder = AsyncTaskMethodBuilderExt.New();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompleteWhenSerialized(Exception? error = null)
    {
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
