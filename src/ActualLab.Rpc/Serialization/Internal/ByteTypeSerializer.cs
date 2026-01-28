using System.Buffers;
using System.Buffers.Binary;
using ActualLab.IO;
using ActualLab.IO.Internal;
using ActualLab.OS;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Serialization.Internal;

public static class ByteTypeSerializer
{
    private static readonly ConcurrentDictionary<Type, ByteString> ToBytesCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<ByteString, Type?> FromBytesCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static ReadOnlySpan<byte> ExpectedTypeSpan => "\0\0"u8;
    public static ReadOnlySpan<byte> NullValueTypeSpan => "\u0001\0"u8;

    public static ByteString ToBytes(Type type) =>
        ToBytesCache.GetOrAdd(type, static t => {
            if (t == typeof(NullValue))
                return NullValueTypeSpan.ToArray().AsByteString();

            var name = new TypeRef(t).WithoutAssemblyVersions().AssemblyQualifiedName;
            var nameSpan = ByteString.FromStringAsUtf8(name).Span;
            var fullLength = nameSpan.Length + 4;

            using var buffer = new ArrayPoolBuffer<byte>(fullLength, false);
            var writer = new SpanWriter(buffer.GetSpan(fullLength));
            if (nameSpan.Length > 0xFFFF)
                throw new ArgumentOutOfRangeException(nameof(type), "Serialized type length exceeds 65535 bytes.");

            BinaryPrimitives.WriteUInt16LittleEndian(writer.Remaining, (ushort)nameSpan.Length); // Length
            BinaryPrimitives.WriteUInt16LittleEndian(writer.Remaining.Slice(2), unchecked((ushort)nameSpan.GetXxHash3L())); // 2-byte hash for faster lookups
            nameSpan.CopyTo(writer.Remaining[4..]);
            buffer.Advance(fullLength);
            return buffer.WrittenSpan.ToArray().AsByteString();
        });

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume RPC-related code is fully preserved")]
    public static Type? FromBytes(ByteString bytes)
        => FromBytesCache.GetOrAdd(bytes, static b => {
            var memory = b.Bytes;
            var length = BinaryPrimitives.ReadUInt16LittleEndian(memory.Span);
            switch (length) {
            case 0:
                return null;
            case 1:
                return typeof(NullValue);
            default:
                var utf8 = new ByteString(memory[4..(length + 4)]);
                var typeRef = new TypeRef(utf8.ToStringAsUtf8());
                return typeRef.Resolve();
            }
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteDerivedItemType(IBufferWriter<byte> buffer, Type expectedType, Type itemType)
    {
        var span = itemType == expectedType
            ? ExpectedTypeSpan
            : ToBytes(itemType).Span;
        buffer.Append(span);
    }

    public static void ReadExactItemType(ref ReadOnlyMemory<byte> data, Type expectedType)
    {
        var itemType = ReadItemType(ref data);
        if (itemType is null || itemType == expectedType)
            return;

        throw Errors.CannotDeserializeUnexpectedArgumentType(expectedType, itemType);
    }

    public static Type ReadDerivedItemType(ref ReadOnlyMemory<byte> data, Type expectedType)
    {
        var itemType = ReadItemType(ref data);
        if (itemType is null)
            return expectedType;
        if (expectedType.IsAssignableFrom(itemType))
            return itemType;
        if (itemType == typeof(NullValue))
            return itemType;

        throw Errors.CannotDeserializeUnexpectedPolymorphicArgumentType(expectedType, itemType);
    }

    public static Type? ReadItemType(ref ReadOnlyMemory<byte> data)
    {
        var length = BinaryPrimitives.ReadUInt16LittleEndian(data.Span);
        switch (length) {
        case 0:
            data = data[2..];
            return null;
        case 1:
            data = data[2..];
            return typeof(NullValue);
        default:
            var fullLength = length + 4;
            var itemType = FromBytes(data[..fullLength].AsByteString());
            data = data[fullLength..];
            return itemType;
        }
    }
}
