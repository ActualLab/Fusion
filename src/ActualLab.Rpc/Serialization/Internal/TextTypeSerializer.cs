using ActualLab.IO.Internal;
using ActualLab.Rpc.Internal;
using Cysharp.Text;

namespace ActualLab.Rpc.Serialization.Internal;

/// <summary>
/// Serializes and deserializes .NET type references as UTF-8 comment-delimited strings for polymorphic RPC arguments.
/// </summary>
public static class TextTypeSerializer
{
    public const int DefaultFromBytesCacheCapacity = 16384;

#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif

    private static readonly ConcurrentDictionary<Type, ByteString> ToBytesCache = new();
    private static PruningCache<ByteString, Type?> _fromBytesCache
        = new(DefaultFromBytesCacheCapacity);

    public static ReadOnlySpan<byte> Prefix => "/* @="u8;
    public static ReadOnlySpan<byte> Suffix => " */"u8;
    public static ReadOnlySpan<byte> ExpectedTypeSpan => "/* @= */"u8; // Must be Prefix + Suffix
    public static ReadOnlySpan<byte> NullValueTypeSpan => "/* @=0 */"u8; // Must be Prefix + Suffix

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
                return new ByteString(NullValueTypeSpan.ToArray());

            var name = new TypeRef(t).WithoutAssemblyVersions().AssemblyQualifiedName;
            using var sb = ZString.CreateUtf8StringBuilder();
            sb.AppendLiteral(Prefix);
            sb.Append(name);
            sb.AppendLiteral(Suffix);
            return new ByteString(sb.AsSpan().ToArray());
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
    public static void WriteDerivedItemType(Utf8TextWriter writer, Type expectedType, Type itemType)
    {
        var span = itemType == expectedType
            ? ExpectedTypeSpan
            : ToBytes(itemType).Span;
        writer.WriteLiteral(span);
    }

    public static void ReadExactItemType(ref ReadOnlyMemory<byte> data, Type expectedType)
    {
        var itemType = ReadItemType(ref data);
        if (itemType is null || itemType == expectedType)
            return;

        throw Errors.CannotDeserializeUnexpectedPolymorphicArgumentType(expectedType, itemType);
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
        if (data.Length < ExpectedTypeSpan.Length)
            throw Errors.InvalidItemTypeFormat();
        if (!data.Span[..Prefix.Length].SequenceEqual(Prefix))
            throw Errors.InvalidItemTypeFormat();

        var suffixIndex = data.Span[Prefix.Length..].IndexOf(Suffix);
        if (suffixIndex < 0)
            throw Errors.InvalidItemTypeFormat();

        if (suffixIndex == 0) {
            data = data[ExpectedTypeSpan.Length..];
            return null;
        }

        var typeLength = Prefix.Length + suffixIndex + Suffix.Length;
        var result = FromBytes(data[..typeLength].AsByteString());
        data = data[typeLength..];
        return result;
    }

    // Private methods

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume RPC-related code is fully preserved")]
    private static Type? Resolve(ByteString marker)
    {
        var span = marker.Span;
        if (span.SequenceEqual(NullValueTypeSpan))
            return typeof(NullValue);
        if (span.Length < ExpectedTypeSpan.Length
            || !span[..Prefix.Length].SequenceEqual(Prefix)
            || !span[^Suffix.Length..].SequenceEqual(Suffix))
            throw Errors.InvalidItemTypeFormat();

        if (span.Length == ExpectedTypeSpan.Length)
            return null;

        var utf8Name = new ByteString(marker.Bytes[Prefix.Length..^Suffix.Length]);
        return new TypeRef(utf8Name.ToStringAsUtf8()).Resolve();
    }
}
