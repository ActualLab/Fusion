using System.ComponentModel;
using ActualLab.Compliance.Internal;
using ActualLab.Conversion;
using MessagePack;

namespace ActualLab.Compliance;

/// <summary>
/// A string that renders as <typeparamref name="TSanitizer"/> masks it while
/// <see cref="Sanitization"/> is active, and as itself otherwise. Wire-compatible with
/// <see cref="string"/> in every format: serialization always carries the raw value, so this
/// protects logs and error messages, not sinks that serialize.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[DataContract, MessagePackFormatter(typeof(SanitizedStringMessagePackFormatter<>))]
[JsonConverter(typeof(SanitizedStringJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(SanitizedStringNewtonsoftJsonConverter))]
[TypeConverter(typeof(SanitizedStringTypeConverter))]
public readonly struct SanitizedString<TSanitizer>(string? value)
    : ISanitizedString, IEquatable<SanitizedString<TSanitizer>>, IConvertibleTo<string>
    where TSanitizer : Sanitizer, new()
{
    public static readonly SanitizedString<TSanitizer> Empty;

    // Closes the open generic CoreModuleInitializer registers, for TFMs with no module initializer
#if !NETSTANDARD2_0
    static SanitizedString()
        => SanitizedStringMemoryPackFormatter.Register<TSanitizer>();
#endif

    [DataMember(Order = 0), Key(0)]
    public string Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => value ?? "";
    }

    [IgnoreDataMember, IgnoreMember]
    Sanitizer ISanitizedString.Sanitizer {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Sanitizer.Get<TSanitizer>();
    }

    [IgnoreDataMember, IgnoreMember]
    public bool IsEmpty {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value.Length == 0;
    }

    public override string ToString()
        => Sanitization.IsActive ? Sanitizer.Get<TSanitizer>().Sanitize(Value) : Value;

    // Conversion

    public static implicit operator SanitizedString<TSanitizer>(string? value) => new(value);

    // Explicit, so reading the raw value can't happen by accident inside an interpolation
    public static explicit operator string(SanitizedString<TSanitizer> value) => value.Value;

    string IConvertibleTo<string>.Convert() => Value;

    // Equality - on the raw value, so two equal secrets stay equal regardless of masking

    public bool Equals(SanitizedString<TSanitizer> other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj)
        => obj is SanitizedString<TSanitizer> other && Equals(other);
    public override int GetHashCode()
        => Value.GetOrdinalHashCode();

    public static bool operator ==(SanitizedString<TSanitizer> left, SanitizedString<TSanitizer> right)
        => left.Equals(right);
    public static bool operator !=(SanitizedString<TSanitizer> left, SanitizedString<TSanitizer> right)
        => !left.Equals(right);
}
