using ActualLab.Collections.Internal;
using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

#pragma warning disable CS0618 // RawItems is obsolete only to discourage non-serialization uses

/// <summary>
/// MemoryPack formatter for <see cref="PropertyBag{TSchema}"/>.
/// </summary>
public sealed class PropertyBagMemoryPackFormatter<TSchema> : VersionTolerantMemoryPackFormatter<PropertyBag<TSchema>>
    where TSchema : TypeSchema
{
    protected override int MemberCount => 1;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        PropertyBag<TSchema> value)
    {
        writer.WriteArray(value.RawItems);
        offsets[0] = writer.WrittenCount;
    }

    protected override PropertyBag<TSchema> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
        => new(deltas[0] != 0 ? reader.ReadArray<PropertyBagItem<TSchema>>() : null);
}

/// <summary>
/// MemoryPack formatter for <see cref="MutablePropertyBag{TSchema}"/>.
/// </summary>
public sealed class MutablePropertyBagMemoryPackFormatter<TSchema>
    : VersionTolerantMemoryPackFormatter<MutablePropertyBag<TSchema>>
    where TSchema : TypeSchema
{
    protected override int MemberCount => 1;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        MutablePropertyBag<TSchema> value)
    {
        writer.WriteArray(value.RawItems);
        offsets[0] = writer.WrittenCount;
    }

    protected override MutablePropertyBag<TSchema> ReadMembers(
        ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
        => new(deltas[0] != 0 ? reader.ReadArray<PropertyBagItem<TSchema>>() : null);
}

/// <summary>
/// MemoryPack formatter for <see cref="PropertyBagItem{TSchema}"/>.
/// </summary>
public sealed class PropertyBagItemMemoryPackFormatter<TSchema>
    : VersionTolerantMemoryPackFormatter<PropertyBagItem<TSchema>>
    where TSchema : TypeSchema
{
    private static MemoryPackFormatter<string> StringFormatter
        => field ??= StringAsSymbolMemoryPackFormatterAttribute.Formatter;

    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        PropertyBagItem<TSchema> value)
    {
        writer.WriteValueWithFormatter(StringFormatter, value.Key);
        offsets[0] = writer.WrittenCount;
        writer.WriteValueWithFormatter(
            TypeDecoratingUniSerializedMemoryPackFormatter<TSchema, object>.Default, value.Serialized);
        offsets[1] = writer.WrittenCount;
    }

    protected override PropertyBagItem<TSchema> ReadMembers(
        ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var key = deltas[0] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<string>, string>(
                StringFormatter)
            : null;
        var serialized = deltas[1] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<TypeDecoratingUniSerialized<TSchema, object>>,
                TypeDecoratingUniSerialized<TSchema, object>>(
                TypeDecoratingUniSerializedMemoryPackFormatter<TSchema, object>.Default)
            : default;
        return new PropertyBagItem<TSchema>(key!, serialized);
    }
}
