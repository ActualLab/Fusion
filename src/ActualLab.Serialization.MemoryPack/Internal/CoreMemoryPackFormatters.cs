using ActualLab.IO;
using MemoryPack.Internal;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// MemoryPack formatter for <see cref="Symbol"/>.
/// </summary>
public sealed class SymbolMemoryPackFormatter : MemoryPackFormatter<Symbol>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Symbol value)
        => writer.WriteStringObject(value.Value);

    public override void Deserialize(ref MemoryPackReader reader, scoped ref Symbol value)
        => value = reader.TryReadStringObject(out var text) ? new Symbol(text) : default;
}

/// <summary>
/// MemoryPack formatter for <see cref="FilePath"/>.
/// </summary>
public sealed class FilePathMemoryPackFormatter : MemoryPackFormatter<FilePath>
{
    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref FilePath value)
        => writer.WriteStringObject(value.Value);

    public override void Deserialize(ref MemoryPackReader reader, scoped ref FilePath value)
        => value = reader.TryReadStringObject(out var text) ? new FilePath(text) : default;
}

/// <summary>
/// MemoryPack formatter for <see cref="VersionSet"/>.
/// </summary>
public sealed class VersionSetMemoryPackFormatter : MemoryPackFormatter<VersionSet>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref VersionSet? value)
    {
        if (value is null)
            writer.WriteNullObjectHeader();
        else
            writer.WriteStringObject(value.Value);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref VersionSet? value)
        => value = reader.TryReadStringObject(out var text) ? new VersionSet(text) : null;
}

/// <summary>
/// MemoryPack formatter for <see cref="JsonString"/>.
/// </summary>
public sealed class JsonStringMemoryPackFormatter : MemoryPackFormatter<JsonString>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref JsonString? value)
    {
        if (value is null)
            writer.WriteNullObjectHeader();
        else
            writer.WriteStringObject(value.Value);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref JsonString? value)
        => value = reader.TryReadStringObject(out var text) ? new JsonString(text!) : null;
}

/// <summary>
/// MemoryPack formatter for <see cref="HostId"/> (<c>GenerateType.Object</c> layout).
/// </summary>
public sealed class HostIdMemoryPackFormatter : MemoryPackFormatter<HostId>
{
    private static MemoryPackFormatter<string> StringFormatter
        => field ??= StringAsSymbolMemoryPackFormatterAttribute.Formatter;

    public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref HostId? value)
    {
        if (value is null) {
            writer.WriteNullObjectHeader();
            return;
        }

        writer.WriteObjectHeader(1);
        writer.WriteValueWithFormatter(StringFormatter, value.Id);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref HostId? value)
    {
        if (!reader.TryReadObjectHeader(out var count)) {
            value = null;
            return;
        }
        if (count > 1)
            MemoryPackSerializationException.ThrowInvalidPropertyCount(typeof(HostId), 1, count);

        var id = count == 1
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<string>, string>(
                StringFormatter)
            : null;
        value = new HostId(id!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="ByteString"/> (<c>GenerateType.Object</c> layout).
/// </summary>
public sealed class ByteStringMemoryPackFormatter : MemoryPackFormatter<ByteString>
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref ByteString value)
    {
        writer.WriteObjectHeader(1);
        writer.WriteValue(value.Bytes);
    }

    public override void Deserialize(ref MemoryPackReader reader, scoped ref ByteString value)
    {
        if (!reader.TryReadObjectHeader(out var count)) {
            value = default;
            return;
        }
        if (count > 1)
            MemoryPackSerializationException.ThrowInvalidPropertyCount(typeof(ByteString), 1, count);

        var bytes = count == 1 ? reader.ReadValue<ReadOnlyMemory<byte>>() : default;
        value = new ByteString(bytes);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="TypeRef"/>.
/// </summary>
public sealed class TypeRefMemoryPackFormatter : VersionTolerantMemoryPackFormatter<TypeRef>
{
    private static MemoryPackFormatter<string> StringFormatter
        => field ??= StringAsSymbolMemoryPackFormatterAttribute.Formatter;

    protected override int MemberCount => 1;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, TypeRef value)
    {
        writer.WriteValueWithFormatter(
            StringFormatter, value.AssemblyQualifiedName);
        offsets[0] = writer.WrittenCount;
    }

    protected override TypeRef ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var assemblyQualifiedName = deltas[0] != 0
            ? reader.ReadValueWithFormatter<IMemoryPackFormatter<string>, string>(
                StringFormatter)
            : null;
        return new TypeRef(assemblyQualifiedName!);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="ExceptionInfo"/>.
/// </summary>
public sealed class ExceptionInfoMemoryPackFormatter : VersionTolerantMemoryPackFormatter<ExceptionInfo>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, ExceptionInfo value)
    {
        writer.WriteValue(value.TypeRef);
        offsets[0] = writer.WrittenCount;
        writer.WriteString(value.Message);
        offsets[1] = writer.WrittenCount;
    }

    protected override ExceptionInfo ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var typeRef = deltas[0] != 0 ? reader.ReadValue<TypeRef>() : default;
        var message = deltas[1] != 0 ? reader.ReadString() : null;
        return new ExceptionInfo(typeRef, message);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="TextOrBytes"/>.
/// </summary>
public sealed class TextOrBytesMemoryPackFormatter : VersionTolerantMemoryPackFormatter<TextOrBytes>
{
    protected override int MemberCount => 2;

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, TextOrBytes value)
    {
        writer.WriteUnmanaged(value.Format);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.Data);
        offsets[1] = writer.WrittenCount;
    }

    protected override TextOrBytes ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var format = deltas[0] != 0 ? reader.ReadUnmanaged<DataFormat>() : default;
        var data = deltas[1] != 0 ? reader.ReadValue<ReadOnlyMemory<byte>>() : default;
        return new TextOrBytes(format, data);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="Result{T}"/>.
/// </summary>
public sealed class ResultMemoryPackFormatter<T> : VersionTolerantMemoryPackFormatter<Result<T>>
{
    protected override int MemberCount => 2;

    public ResultMemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, Result<T> value)
    {
        writer.WriteValue(value.ValueOrDefault);
        offsets[0] = writer.WrittenCount;
        writer.WriteValue(value.ExceptionInfo);
        offsets[1] = writer.WrittenCount;
    }

    protected override Result<T> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var valueOrDefault = deltas[0] != 0 ? reader.ReadValue<T>() : default;
        var exceptionInfo = deltas[1] != 0 ? reader.ReadValue<ExceptionInfo?>() : null;
        return new Result<T>(valueOrDefault!, exceptionInfo);
    }
}

/// <summary>
/// MemoryPack formatter for <see cref="Box{T}"/>.
/// </summary>
public sealed class BoxMemoryPackFormatter<T> : VersionTolerantMemoryPackFormatter<Box<T>>
{
    protected override int MemberCount => 1;

    public BoxMemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, Box<T> value)
    {
        writer.WriteValue(value.Value);
        offsets[0] = writer.WrittenCount;
    }

    protected override Box<T> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
        => new(deltas[0] != 0 ? reader.ReadValue<T>()! : default!);
}

/// <summary>
/// MemoryPack formatter for <see cref="MutableBox{T}"/>.
/// </summary>
public sealed class MutableBoxMemoryPackFormatter<T> : VersionTolerantMemoryPackFormatter<MutableBox<T>>
{
    protected override int MemberCount => 1;

    public MutableBoxMemoryPackFormatter()
        => MemoryPackSerialization.Register<T>();

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets, MutableBox<T> value)
    {
        writer.WriteValue(value.Value);
        offsets[0] = writer.WrittenCount;
    }

    protected override MutableBox<T> ReadMembers(ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
        => new(deltas[0] != 0 ? reader.ReadValue<T>()! : default!);
}

/// <summary>
/// MemoryPack formatter for <see cref="ImmutableBimap{TFrom,TTo}"/>.
/// </summary>
public sealed class ImmutableBimapMemoryPackFormatter<TFrom, TTo>
    : VersionTolerantMemoryPackFormatter<ImmutableBimap<TFrom, TTo>>
    where TFrom : notnull
    where TTo : notnull
{
    protected override int MemberCount => 1;

    public ImmutableBimapMemoryPackFormatter()
    {
        MemoryPackSerialization.Register<TFrom>();
        MemoryPackSerialization.Register<TTo>();
    }

    protected override void WriteMembers(
        ref MemoryPackWriter<ReusableLinkedArrayBufferWriter> writer, scoped Span<int> offsets,
        ImmutableBimap<TFrom, TTo> value)
    {
        writer.WriteValue(value.Forward);
        offsets[0] = writer.WrittenCount;
    }

    protected override ImmutableBimap<TFrom, TTo> ReadMembers(
        ref MemoryPackReader reader, scoped ReadOnlySpan<int> deltas)
    {
        var forward = deltas[0] != 0 ? reader.ReadValue<IReadOnlyDictionary<TFrom, TTo>>() : null;
        return new ImmutableBimap<TFrom, TTo>(forward!);
    }
}
