using ActualLab.Interception;
using ActualLab.IO;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcInboundMessage(
    byte callTypeId,
    long relatedId,
    RpcMethodRef methodRef,
    ReadOnlyMemory<byte> argumentData,
    RpcHeader[]? headers,
    ArrayPoolArrayRef<byte> bufferRef)
{
    private ArrayPoolArrayRef<byte> _bufferRef = bufferRef;

    public readonly byte CallTypeId = callTypeId;
    public readonly long RelatedId = relatedId;
    public readonly RpcMethodRef MethodRef = methodRef;
    public readonly RpcHeader[]? Headers = headers;
    public readonly ReadOnlyMemory<byte> ArgumentData = argumentData;
    public ArgumentList? Arguments;

    public void MarkProcessed()
    {
        _bufferRef.Dispose();
        _bufferRef = default;
    }

    public override string ToString()
    {
        var headers = Headers.OrEmpty();
        return $"{nameof(RpcInboundMessage)} #{RelatedId}/{CallTypeId}: {MethodRef.FullName}, "
            + (Arguments is not null
                ? $"Arguments: {Arguments}"
                : _bufferRef.IsNone
                    ? "ArgumentData: already released"
                    : $"ArgumentData: {new ByteString(ArgumentData).ToString(16)}")
            + (headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "")
            + (_bufferRef.Handle is not null ? " [attached]" : " [detached]");
    }

    // This record relies on referential equality
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
