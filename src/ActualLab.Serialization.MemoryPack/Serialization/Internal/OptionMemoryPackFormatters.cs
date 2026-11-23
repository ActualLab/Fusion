using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

#pragma warning disable CS0618 // RawHasValue is obsolete only to discourage non-serialization uses

/// <summary>
/// MemoryPack formatter for <see cref="Option{T}"/>.
/// </summary>
public sealed class OptionMemoryPackFormatter<T> : VersionTolerantMemoryPackFormatter<Option<T>>
{
    public static readonly OptionMemoryPackFormatter<T> Default = new();

    protected override int MemberCount => 2;

    public OptionMemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, Option<T> value)
    {
        writer.WriteUnmanaged(value.HasValue);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.ValueOrDefault);
        offsets[1] = writer.WrittenCount;
    }

    protected override Option<T> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var hasValue = deltas[0] != 0 && reader.ReadUnmanaged<bool>();
        var valueOrDefault = deltas[1] != 0 ? reader.ReadValue<T>() : default;
        return new Option<T>(hasValue, valueOrDefault);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="ApiOption{T}"/>.
/// </summary>
public sealed class ApiOptionMemoryPackFormatter<T> : VersionTolerantMemoryPackFormatter<ApiOption<T>>
{
    protected override int MemberCount => 2;

    public ApiOptionMemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, ApiOption<T> value)
    {
        writer.WriteUnmanaged(value.HasValue);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.ValueOrDefault);
        offsets[1] = writer.WrittenCount;
    }

    protected override ApiOption<T> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var hasValue = deltas[0] != 0 && reader.ReadUnmanaged<bool>();
        var valueOrDefault = deltas[1] != 0 ? reader.ReadValue<T>() : default;
        return new ApiOption<T>(hasValue, valueOrDefault!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="ApiNullable{T}"/>.
/// </summary>
public sealed class ApiNullableMemoryPackFormatter<T> : VersionTolerantMemoryPackFormatter<ApiNullable<T>>
    where T : struct
{
    protected override int MemberCount => 2;

    public ApiNullableMemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, ApiNullable<T> value)
    {
        writer.WriteUnmanaged(value.HasValue);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.ValueOrDefault);
        offsets[1] = writer.WrittenCount;
    }

    protected override ApiNullable<T> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var hasValue = deltas[0] != 0 && reader.ReadUnmanaged<bool>();
        var valueOrDefault = deltas[1] != 0 ? reader.ReadValue<T>() : default;
        return new ApiNullable<T>(hasValue, valueOrDefault);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="ApiNullable8{T}"/>.
/// </summary>
public sealed class ApiNullable8MemoryPackFormatter<T> : VersionTolerantMemoryPackFormatter<ApiNullable8<T>>
    where T : struct
{
    protected override int MemberCount => 2;

    public ApiNullable8MemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, ApiNullable8<T> value)
    {
        writer.WriteUnmanaged(value.RawHasValue);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.ValueOrDefault);
        offsets[1] = writer.WrittenCount;
    }

    protected override ApiNullable8<T> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var rawHasValue = deltas[0] != 0 ? reader.ReadUnmanaged<long>() : 0L;
        var valueOrDefault = deltas[1] != 0 ? reader.ReadValue<T>() : default;
        return new ApiNullable8<T>(rawHasValue, valueOrDefault);
    }
}
