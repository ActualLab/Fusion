using ActualLab.Rpc;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// MemoryPack formatter for <see cref="RpcMethodRef"/> (<c>GenerateType.Object</c> layout).
/// </summary>
public sealed class RpcMethodRefMemoryPackFormatter : MemoryPackFormatter<RpcMethodRef>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref RpcMethodRef value)
    {
        writer.WriteObjectHeader(1);
        writer.WriteValue(value.Utf8Name);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref RpcMethodRef value)
    {
        if (!reader.TryReadObjectHeader(out var count)) {
            value = default;
            return;
        }
        if (count > 1)
            MemoryPackSerializationException.ThrowInvalidPropertyCount(typeof(RpcMethodRef), 1, count);

        var utf8Name = count == 1 ? reader.ReadValue<ReadOnlyMemory<byte>>() : default;
        value = new RpcMethodRef(utf8Name);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="RpcHeaderKey"/> (<c>GenerateType.Object</c> layout).
/// </summary>
public sealed class RpcHeaderKeyMemoryPackFormatter : MemoryPackFormatter<RpcHeaderKey>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref RpcHeaderKey value)
    {
        writer.WriteObjectHeader(1);
        writer.WriteValue(value.Utf8Name);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref RpcHeaderKey value)
    {
        if (!reader.TryReadObjectHeader(out var count)) {
            value = default;
            return;
        }
        if (count > 1)
            MemoryPackSerializationException.ThrowInvalidPropertyCount(typeof(RpcHeaderKey), 1, count);

        var utf8Name = count == 1 ? reader.ReadValue<ReadOnlyMemory<byte>>() : default;
        value = new RpcHeaderKey(utf8Name);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="RpcHeader"/>.
/// </summary>
public sealed class RpcHeaderMemoryPackFormatter : VersionTolerantMemoryPackFormatter<RpcHeader>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, RpcHeader value)
    {
        writer.WriteString(value.Name);
        offsets[0] = writer.WrittenCount;
        writer.WriteString(value.Value);
        offsets[1] = writer.WrittenCount;
    }

    protected override RpcHeader ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var name = deltas[0] != 0 ? reader.ReadString() : null;
        var value = deltas[1] != 0 ? reader.ReadString() : null;
        return new RpcHeader(name!, value);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="RpcHandshake"/>.
/// </summary>
public sealed class RpcHandshakeMemoryPackFormatter : VersionTolerantMemoryPackFormatter<RpcHandshake>
{
    protected override int MemberCount => 6;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, RpcHandshake value)
    {
        writer.WriteUnmanaged(value.RemotePeerId);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.RemoteApiVersionSet);
        offsets[1] = writer.WrittenCount;
        writer.WriteUnmanaged(value.RemoteHubId);
        offsets[2] = writer.WrittenCount;
        writer.WriteUnmanaged(value.ProtocolVersion);
        offsets[3] = writer.WrittenCount;
        writer.WriteUnmanaged(value.Index);
        offsets[4] = writer.WrittenCount;
        writer.WriteString(value.Secret);
        offsets[5] = writer.WrittenCount;
    }

    protected override RpcHandshake ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var remotePeerId = deltas[0] != 0 ? reader.ReadUnmanaged<Guid>() : default;
        var remoteApiVersionSet = deltas[1] != 0 ? reader.ReadValue<VersionSet>() : null;
        var remoteHubId = deltas[2] != 0 ? reader.ReadUnmanaged<Guid>() : default;
        var protocolVersion = deltas[3] != 0 ? reader.ReadUnmanaged<int>() : 0;
        var index = deltas[4] != 0 ? reader.ReadUnmanaged<int>() : 0;
        var secret = deltas[5] != 0 ? reader.ReadString() : null;
        return new RpcHandshake(remotePeerId, remoteApiVersionSet, remoteHubId, protocolVersion, index, secret);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="RpcCacheKey"/> (<c>GenerateType.Object</c> layout).
/// </summary>
public sealed class RpcCacheKeyMemoryPackFormatter : MemoryPackFormatter<RpcCacheKey>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref RpcCacheKey? value)
    {
        if (value is null) {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(2);
        writer.WriteString(value.Name);
        writer.WriteValue(value.ArgumentData);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref RpcCacheKey? value)
    {
        if (!reader.TryReadObjectHeader(out var count)) {
            value = null;
            return;
        }
        if (count > 2)
            MemoryPackSerializationException.ThrowInvalidPropertyCount(typeof(RpcCacheKey), 2, count);

        var name = count >= 1 ? reader.ReadString() : null;
        var argumentData = count >= 2 ? reader.ReadValue<ReadOnlyMemory<byte>>() : default;
        value = new RpcCacheKey(name!, argumentData);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="RpcCacheValue"/>.
/// </summary>
public sealed class RpcCacheValueMemoryPackFormatter : VersionTolerantMemoryPackFormatter<RpcCacheValue>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, RpcCacheValue value)
    {
        writer.WriteValue(value.Data);
        offsets[0] = writer.WrittenCount;
        writer.WriteString(value.Hash);
        offsets[1] = writer.WrittenCount;
    }

    protected override RpcCacheValue ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var data = deltas[0] != 0 ? reader.ReadValue<ReadOnlyMemory<byte>>() : default;
        var hash = deltas[1] != 0 ? reader.ReadString() : null;
        return new RpcCacheValue(data, hash!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="RpcStream{T}"/>. Its members are unmanaged,
/// so the member byte lengths are known upfront and no temporary buffer is needed.
/// </summary>
public sealed class RpcStreamMemoryPackFormatter<T> : MemoryPackFormatter<RpcStream<T>>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref RpcStream<T>? value)
    {
        if (value is null) {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(5);
        writer.WriteVarInt(Unsafe.SizeOf<int>());
        writer.WriteVarInt(Unsafe.SizeOf<int>());
        writer.WriteVarInt(Unsafe.SizeOf<RpcObjectId>());
        writer.WriteVarInt(Unsafe.SizeOf<bool>());
        writer.WriteVarInt(Unsafe.SizeOf<bool>());
        // SerializedId registers the stream on the first read, so it must be read exactly once
        writer.WriteUnmanaged(
            value.AckPeriod, value.AckAdvance, value.SerializedId, value.AllowReconnect, value.IsRealTime);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref RpcStream<T>? value)
    {
        if (!reader.TryReadObjectHeader(out var count)) {
            value = null;
            return;
        }

        Span<int> deltas = stackalloc int[Math.Max((int)count, 5)];
        deltas.Clear();
        for (var i = 0; i < count; i++)
            deltas[i] = reader.ReadVarIntInt32();

        var ackPeriod = deltas[0] != 0 ? reader.ReadUnmanaged<int>() : 0;
        var ackAdvance = deltas[1] != 0 ? reader.ReadUnmanaged<int>() : 0;
        var serializedId = deltas[2] != 0 ? reader.ReadUnmanaged<RpcObjectId>() : default;
        var allowReconnect = deltas[3] != 0 && reader.ReadUnmanaged<bool>();
        var isRealTime = deltas[4] != 0 && reader.ReadUnmanaged<bool>();
        for (var i = 5; i < count; i++)
            reader.Advance(deltas[i]);

        value = new RpcStream<T>() {
            AckPeriod = ackPeriod,
            AckAdvance = ackAdvance,
            SerializedId = serializedId,
            AllowReconnect = allowReconnect,
            IsRealTime = isRealTime,
        };
    }
}
