using ActualLab.IO.Internal;
using ActualLab.Rpc.Internal;
using Cysharp.Text;

namespace ActualLab.Rpc.Serialization.Internal;

/// <summary>
/// Serializes and deserializes .NET type references as UTF-8 comment-delimited strings for polymorphic RPC arguments.
/// </summary>
public static class TextTypeSerializer
{
    private static readonly ConcurrentDictionary<Type, ByteString> ToBytesCache = new();
    private static readonly ConcurrentDictionary<ByteString, Type?> FromBytesCache = new();

    public static ReadOnlySpan<byte> Prefix => "/* @="u8;
    public static ReadOnlySpan<byte> Suffix => " */"u8;
    public static ReadOnlySpan<byte> ExpectedTypeSpan => "/* @= */"u8; // Must be Prefix + Suffix
    public static ReadOnlySpan<byte> NullValueTypeSpan => "/* @=0 */"u8; // Must be Prefix + Suffix

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

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume RPC-related code is fully preserved")]
    public static Type? FromBytes(ByteString bytes)
        => FromBytesCache.GetOrAdd(bytes, static b => {
            if (b.Span.SequenceEqual(NullValueTypeSpan))
                return typeof(NullValue);

            var utf8Name = new ByteString(b.Bytes[Prefix.Length..^Suffix.Length]);
            var typeRef = new TypeRef(utf8Name.ToStringAsUtf8());
            return typeRef.Resolve();
        });

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
}
