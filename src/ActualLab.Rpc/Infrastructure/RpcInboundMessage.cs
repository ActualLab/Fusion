using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public sealed class RpcInboundMessage(
    byte callTypeId,
    long relatedId,
    RpcMethodRef methodRef,
    ReadOnlyMemory<byte> argumentData,
    RpcHeader[]? headers)
{
    public readonly byte CallTypeId = callTypeId;
    public readonly long RelatedId = relatedId;
    public readonly RpcMethodRef MethodRef = methodRef;
    public readonly RpcHeader[]? Headers = headers;
    public ReadOnlyMemory<byte> ArgumentData = argumentData;
    public ArgumentList? Arguments;

    public override string ToString()
    {
        var headers = Headers.OrEmpty();
        return $"{nameof(RpcInboundMessage)} #{RelatedId}/{CallTypeId}: {MethodRef.FullName}, "
            + (Arguments is not null
                ? $"Arguments: {Arguments}, "
                : $"ArgumentData: {new ByteString(ArgumentData).ToString(16)}, ")
            + $", Headers: {headers.ToDelimitedString()}";
    }

    // This record relies on referential equality
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
