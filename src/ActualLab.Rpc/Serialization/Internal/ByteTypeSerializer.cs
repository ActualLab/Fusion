using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.OS;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Serialization.Internal;

public static class ByteTypeSerializer
{
    private static readonly ConcurrentDictionary<Type, ByteString> ToBytesCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<ByteString, Type?> FromBytesCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static readonly byte[] NullTypeBytes = [0, 0];
    public static ReadOnlySpan<byte> NullTypeSpan => NullTypeBytes;

    public static ByteString ToBytes(Type type) =>
        ToBytesCache.GetOrAdd(type, static t => {
            var name = new TypeRef(t).WithoutAssemblyVersions().AssemblyQualifiedName;
            var nameSpan = ByteString.FromStringAsUtf8(name).Span;
            var fullLength = nameSpan.Length + 4;

            using var buffer = new ArrayPoolBuffer<byte>(fullLength, false);
            var writer = new SpanWriter(buffer.GetSpan(fullLength));
            if (nameSpan.Length > 0xFFFF)
                throw new ArgumentOutOfRangeException(nameof(type), "Serialized type length exceeds 65535 bytes.");

            writer.Remaining.WriteUnchecked((ushort)nameSpan.Length); // Length
            writer.Remaining.WriteUnchecked(2, unchecked((ushort)nameSpan.GetXxHash3L())); // 2-byte hash for faster lookups
            nameSpan.CopyTo(writer.Remaining[4..]);
            buffer.Advance(fullLength);
            return buffer.WrittenSpan.ToArray().AsByteString();
        });

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume RPC-related code is fully preserved")]
    public static Type? FromBytes(ByteString bytes)
        => FromBytesCache.GetOrAdd(bytes, static b => {
            var memory = b.Bytes;
            var length = memory.Span.ReadUnchecked<ushort>();
            if (length == 0)
                return null;

            var utf8 = new ByteString(memory[4..(length + 4)]);
            var typeRef = new TypeRef(utf8.ToStringAsUtf8());
            return typeRef.Resolve();
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteDerivedItemType(IBufferWriter<byte> buffer, Type expectedType, Type itemType)
    {
        var span = itemType == expectedType
            ? NullTypeSpan
            : ToBytes(itemType).Span;
        buffer.Append(span);
    }

    public static void ReadExactItemType(ref ReadOnlyMemory<byte> data, Type expectedType)
    {
        var itemType = ReadItemType(ref data);
        if (itemType == null || itemType == expectedType)
            return;

        throw Errors.CannotDeserializeUnexpectedArgumentType(expectedType, itemType);
    }

    public static Type ReadDerivedItemType(ref ReadOnlyMemory<byte> data, Type expectedType)
    {
        var itemType = ReadItemType(ref data);
        if (itemType == null)
            return expectedType;
        if (expectedType.IsAssignableFrom(itemType))
            return itemType;

        throw Errors.CannotDeserializeUnexpectedPolymorphicArgumentType(expectedType, itemType);
    }

    public static Type? ReadItemType(ref ReadOnlyMemory<byte> data)
    {
        var length = data.Span.ReadUnchecked<ushort>();
        if (length == 0) {
            data = data[2..];
            return null;
        }

        var fullLength = length + 4;
        var itemType = FromBytes(data[..fullLength].AsByteString());
        data = data[fullLength..];
        return itemType;
    }
}
