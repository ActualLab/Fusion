using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A base for hand-written MemoryPack formatters producing exactly what the MemoryPack generator
/// emits for <c>GenerateType.VersionTolerant</c>: an object header, a byte-length delta per member,
/// and then the members. A <c>null</c> reference is written as a null object header.
/// </summary>
public abstract class VersionTolerantMemoryPackFormatter<T> : MemoryPackFormatter<T>
{
    protected abstract int MemberCount { get; }

    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref T? value)
    {
        if (value is null) {
            writer.WriteNullObjectHeader();
            return;
        }

        var memberCount = MemberCount;
        var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();
        try {
            Span<int> offsets = stackalloc int[memberCount];
            var tempWriter = new MemoryPackWriter<ReusableLinkedArrayBufferWriter>(
                ref tempBuffer, writer.OptionalState);
            WriteMembers(ref tempWriter, offsets, value);
            tempWriter.Flush();

            writer.WriteObjectHeader((byte)memberCount);
            var lastOffset = 0;
            foreach (var offset in offsets) {
                writer.WriteVarInt(offset - lastOffset);
                lastOffset = offset;
            }
            tempBuffer.WriteToAndReset(ref writer);
        }
        finally {
            ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);
        }
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref T? value)
    {
        if (!reader.TryReadObjectHeader(out var count)) {
            value = default;
            return;
        }

        var memberCount = MemberCount;
        // Members missing on the wire (an older peer) have zero deltas, i.e. read as default
        Span<int> deltas = stackalloc int[Math.Max(count, memberCount)];
        deltas.Clear();
        for (var i = 0; i < count; i++)
            deltas[i] = reader.ReadVarIntInt32();

        value = ReadMembers(ref reader, deltas);
        for (var i = memberCount; i < count; i++)
            reader.Advance(deltas[i]);
    }

    // Protected methods

    // Must store writer.WrittenCount into offsets[i] right after writing member i
    protected abstract void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, T value);

    // deltas[i] == 0 means member i is absent and must be left default
    protected abstract T ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas);
}
