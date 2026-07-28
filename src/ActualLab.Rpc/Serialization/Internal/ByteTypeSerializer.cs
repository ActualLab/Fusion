using System.Buffers;
using System.Buffers.Binary;
using ActualLab.IO.Internal;
using ActualLab.OS;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Serialization.Internal;

/// <summary>
/// Serializes and deserializes .NET type references as compact binary representations for polymorphic RPC arguments.
/// </summary>
public static class ByteTypeSerializer
{
    public const int DefaultFromBytesCacheCapacity = 16384;

#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif

    private static readonly ConcurrentDictionary<Type, ByteString> ToBytesCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static PruningCache<ByteString, Type?> _fromBytesCache
        = new(DefaultFromBytesCacheCapacity);

    public static ReadOnlySpan<byte> ExpectedTypeSpan => "\0\0"u8;
    public static ReadOnlySpan<byte> NullValueTypeSpan => "\u0001\0"u8;

    public static int FromBytesCacheCapacity {
        get => _fromBytesCache.Capacity;
        set {
            lock (StaticLock)
                _fromBytesCache = new PruningCache<ByteString, Type?>(value);
        }
    }
    public static int FromBytesCacheSize => _fromBytesCache.Count;

    public static ByteString ToBytes(Type type) =>
        ToBytesCache.GetOrAdd(type, static t => {
            if (t == typeof(NullValue))
                return NullValueTypeSpan.ToArray().AsByteString();

            var name = new TypeRef(t).WithoutAssemblyVersions().AssemblyQualifiedName;
            var nameSpan = ByteString.FromStringAsUtf8(name).Span;
            var fullLength = nameSpan.Length + 4;

            var buffer = new RefArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, fullLength, mustClear: false);
            try {
                var writer = new SpanWriter(buffer.GetSpan(fullLength));
                if (nameSpan.Length > 0xFFFF)
                    throw new ArgumentOutOfRangeException(nameof(type), "Serialized type length exceeds 65535 bytes.");

                BinaryPrimitives.WriteUInt16LittleEndian(writer.Remaining, (ushort)nameSpan.Length); // Length
                BinaryPrimitives.WriteUInt16LittleEndian(writer.Remaining.Slice(2), unchecked((ushort)nameSpan.GetXxHash3L())); // 2-byte name hash
                nameSpan.CopyTo(writer.Remaining[4..]);
                buffer.Advance(fullLength);
                return buffer.WrittenSpan.ToArray().AsByteString();
            }
            finally {
                buffer.Release();
            }
        });

    public static Type? FromBytes(ByteString bytes)
    {
        // bytes usually projects into a pooled transport buffer that's recycled right after the
        // frame is parsed, so we probe the cache first and store an owned copy only on a miss.
        var fromBytesCache = _fromBytesCache;
        if (fromBytesCache.TryGet(bytes, out var cachedType))
            return cachedType;

        var type = Resolve(bytes);
        fromBytesCache.TryAdd(new ByteString(bytes.Bytes.ToArray()), type);
        return type;
    }

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
        if (data.Length < 2)
            throw Errors.InvalidItemTypeFormat();

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
            if (data.Length < fullLength)
                throw Errors.InvalidItemTypeFormat();

            var itemType = FromBytes(data[..fullLength].AsByteString());
            data = data[fullLength..];
            return itemType;
        }
    }

    // Private methods

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume RPC-related code is fully preserved")]
    private static Type? Resolve(ByteString marker)
    {
        var span = marker.Span;
        if (span.Length < 2)
            throw Errors.InvalidItemTypeFormat();

        var length = BinaryPrimitives.ReadUInt16LittleEndian(span);
        switch (length) {
        case 0:
        case 1:
            if (span.Length != 2)
                throw Errors.InvalidItemTypeFormat();

            return length == 0 ? null : typeof(NullValue);
        default:
            if (span.Length != length + 4)
                throw Errors.InvalidItemTypeFormat();

            var utf8Name = new ByteString(marker.Bytes[4..]);
            var expectedHash = unchecked((ushort)utf8Name.Span.GetXxHash3L());
            if (BinaryPrimitives.ReadUInt16LittleEndian(span[2..]) != expectedHash)
                throw Errors.InvalidItemTypeFormat();

            return new TypeRef(utf8Name.ToStringAsUtf8()).Resolve();
        }
    }
}
