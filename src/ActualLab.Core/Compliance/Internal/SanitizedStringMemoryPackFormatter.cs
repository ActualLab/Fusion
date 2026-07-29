using MemoryPack;

namespace ActualLab.Compliance.Internal;

#if !NETSTANDARD2_0

/// <summary>
/// MemoryPack formatter for <see cref="SanitizedString{TSanitizer}"/> - writes the raw value
/// exactly as a <see cref="string"/> would be written.
/// </summary>
/// <remarks>
/// MemoryPack is the one format where wire compatibility with <see cref="string"/> can't come
/// from an attribute: a [MemoryPackable] struct emits an object header plus members, not a bare
/// string. Hence an explicit formatter, registered for the open generic type.
/// </remarks>
public sealed class SanitizedStringMemoryPackFormatter<TSanitizer> : MemoryPackFormatter<SanitizedString<TSanitizer>>
    where TSanitizer : Sanitizer, new()
{
    public override void Serialize<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer, scoped ref SanitizedString<TSanitizer> value)
        => writer.WriteString(value.Value);

    public override void Deserialize(
        ref MemoryPackReader reader, scoped ref SanitizedString<TSanitizer> value)
        => value = new SanitizedString<TSanitizer>(reader.ReadString());
}

/// <summary>
/// Registration helper for <see cref="SanitizedStringMemoryPackFormatter{TSanitizer}"/>.
/// </summary>
public static class SanitizedStringMemoryPackFormatter
{
    /// <summary>
    /// Registers the formatter for one closed <see cref="SanitizedString{TSanitizer}"/>.
    /// Idempotent - MemoryPack's provider keeps the last registration, and every one of these
    /// is equivalent.
    /// </summary>
    public static void Register<TSanitizer>()
        where TSanitizer : Sanitizer, new()
        => MemoryPackFormatterProvider.Register(new SanitizedStringMemoryPackFormatter<TSanitizer>());

    /// <summary>Registers the open generic, so any instantiation resolves without being touched first.</summary>
    public static void RegisterGenericType()
        => MemoryPackFormatterProvider.RegisterGenericType(
            typeof(SanitizedString<>), typeof(SanitizedStringMemoryPackFormatter<>));
}

#endif
