using System.Buffers;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Serialization.Internal;

public static class TypeSerializer
{
    private static readonly ConcurrentDictionary<Type, ByteString> ToBytesCache = new();
    private static readonly ConcurrentDictionary<ByteString, Type?> FromBytesCache = new();

    public static readonly byte[] NullTypeBytes = [0, 0];
    public static ReadOnlySpan<byte> NullTypeSpan => NullTypeBytes.AsSpan();

    public static ByteString ToBytes(Type type) =>
        ToBytesCache.GetOrAdd(type, t => {
            var name = new TypeRef(t).WithoutAssemblyVersions().AssemblyQualifiedName.Value;
            var nameSpan = ByteString.FromStringAsUtf8(name).Span;
            var fullLength = nameSpan.Length + 4;

            using var buffer = new ArrayPoolBuffer<byte>(fullLength);
            var writer = new SpanWriter(buffer.GetSpan(fullLength));
            if (nameSpan.Length > 0xFFFF)
                throw new ArgumentOutOfRangeException(nameof(type), "Serialized type length exceeds 65535 bytes.");

            writer.Remaining.WriteUnchecked((ushort)nameSpan.Length); // Length
            writer.Remaining.WriteUnchecked(2, unchecked((ushort)nameSpan.GetXxHash3L())); // 2-byte hash for faster lookups
            nameSpan.CopyTo(writer.Remaining[4..]);
            buffer.Advance(fullLength);
            return buffer.WrittenSpan.ToArray();
        });

    public static Type? FromBytes(ByteString bytes)
        => FromBytesCache.GetOrAdd(bytes, b => {
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
        var length = data.Span.ReadUnchecked<ushort>();
        if (length == 0) {
            data = data[2..];
            return;
        }

        var fullLength = length + 4;
        var itemType = FromBytes(data[..fullLength]);
        data = data[fullLength..];
        if (itemType == null)
            return;
        if (expectedType == itemType)
            return;

        throw Errors.CannotDeserializeUnexpectedPolymorphicArgumentType(expectedType, itemType);
    }

    public static Type ReadDerivedItemType(ref ReadOnlyMemory<byte> data, Type expectedType)
    {
        var length = data.Span.ReadUnchecked<ushort>();
        if (length == 0) {
            data = data[2..];
            return expectedType;
        }

        var fullLength = length + 4;
        var itemType = FromBytes(data[..fullLength]);
        data = data[fullLength..];
        if (itemType == null)
            return expectedType;
        if (expectedType.IsAssignableFrom(itemType))
            return itemType;

        throw Errors.CannotDeserializeUnexpectedPolymorphicArgumentType(expectedType, itemType);
    }
}
