using ActualLab.IO.Internal;
using ActualLab.Rpc.Internal;
using Cysharp.Text;

namespace ActualLab.Rpc.Serialization.Internal;

public static class TextTypeSerializer
{
    private static readonly ConcurrentDictionary<Type, ByteString> ToBytesCache = new();
    private static readonly ConcurrentDictionary<ByteString, Type?> FromBytesCache = new();

    public static ReadOnlySpan<byte> Prefix => "/* @="u8;
    public static ReadOnlySpan<byte> Suffix => " */"u8;
    public static ReadOnlySpan<byte> NullTypeSpan => "/* @= */"u8; // Must be Prefix + Suffix

    public static ByteString ToBytes(Type type) =>
        ToBytesCache.GetOrAdd(type, static t => {
            var name = new TypeRef(t).WithoutAssemblyVersions().AssemblyQualifiedName.Value;
            using var sb = ZString.CreateUtf8StringBuilder();
            sb.AppendLiteral(Prefix);
            sb.Append(name);
            sb.AppendLiteral(Suffix);
            return new ByteString(sb.AsSpan().ToArray());
        });

    public static Type? FromBytes(ByteString bytes)
        => FromBytesCache.GetOrAdd(bytes, static b => {
            var utf8Name = new ByteString(b.Bytes[Prefix.Length..^Suffix.Length]);
            var typeRef = new TypeRef(utf8Name.ToStringAsUtf8());
            return typeRef.Resolve();
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteDerivedItemType(Utf8TextWriter writer, Type expectedType, Type itemType)
    {
        var span = itemType == expectedType
            ? NullTypeSpan
            : ToBytes(itemType).Span;
        writer.WriteLiteral(span);
    }

    public static void ReadExactItemType(ref ReadOnlyMemory<byte> data, Type expectedType)
    {
        var itemType = ReadItemType(ref data);
        if (itemType == null || itemType == expectedType)
            return;

        throw Errors.CannotDeserializeUnexpectedPolymorphicArgumentType(expectedType, itemType);
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
        if (data.Length < NullTypeSpan.Length)
            throw Errors.InvalidItemTypeFormat();
        if (!data.Span[..Prefix.Length].SequenceEqual(Prefix))
            throw Errors.InvalidItemTypeFormat();

        var suffixIndex = data.Span[Prefix.Length..].IndexOf(Suffix);
        if (suffixIndex < 0)
            throw Errors.InvalidItemTypeFormat();

        if (suffixIndex == 0) {
            data = data[NullTypeSpan.Length..];
            return null;
        }

        var typeLength = Prefix.Length + suffixIndex + Suffix.Length;
        var result = FromBytes(data[..typeLength].AsByteString());
        data = data[typeLength..];
        return result;
    }
}
