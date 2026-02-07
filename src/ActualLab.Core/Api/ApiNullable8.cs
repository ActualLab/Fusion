using System.Diagnostics;
using ActualLab.Api.Internal;
using ActualLab.Conversion;
using MessagePack;

namespace ActualLab.Api;

#pragma warning disable CA1036
#pragma warning disable CS0618 // Type or member is obsolete

/// <summary>
/// Factory methods for creating <see cref="ApiNullable8{T}"/> instances.
/// </summary>
public static class ApiNullable8
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiNullable8<T> Null<T>() where T : struct => default;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiNullable8<T> Value<T>(T value) where T : struct => new(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiNullable8<T> From<T>(T? value) where T : struct => new(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiNullable<T> From<T>(Option<T> value)
        where T : struct
        => value.IsSome(out var v) ? new(true, v) : default;
}

/// <summary>
/// Like <see cref="Nullable{T}"/>, but with 8-byte HasValue.
/// </summary>
/// <typeparam name="T">The type of <see cref="Value"/>.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 8)] // Important!
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[MessagePackFormatter(typeof(ApiNullable8MessagePackFormatter<>))]
[DebuggerDisplay("{" + nameof(DebugValue) + "}")]
public readonly partial struct ApiNullable8<T>
    : IEquatable<ApiNullable8<T>>, IComparable<ApiNullable8<T>>,
        IConvertibleTo<T?>, IConvertibleTo<Option<T>>
    where T : struct
{
    public static readonly ApiNullable8<T> Null;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    private string DebugValue => ToString();

    [Obsolete("This member exists solely to make serialization work. Don't use it!")]
    [DataMember(Order = 0), MemoryPackOrder(0), JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public long RawHasValue { get; }

    [DataMember(Order = 1), MemoryPackOrder(1), JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public T ValueOrDefault { get; }

    [JsonInclude, Newtonsoft.Json.JsonProperty, MemoryPackIgnore, IgnoreMember]
    public T? Value => HasValue ? ValueOrDefault : null;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool HasValue {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RawHasValue != 0;
    }

    // Constructors

    [JsonConstructor, Newtonsoft.Json.JsonConstructor]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ApiNullable8(T? value)
    {
        RawHasValue = value.HasValue ? 1 : 0;
        ValueOrDefault = value.GetValueOrDefault();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ApiNullable8(bool hasValue, T value)
    {
        RawHasValue = hasValue ? 1 : 0;
        ValueOrDefault = value;
    }

    [MemoryPackConstructor, SerializationConstructor]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once ConvertToPrimaryConstructor
    public ApiNullable8(long rawHasValue, T valueOrDefault)
    {
        RawHasValue = rawHasValue;
        ValueOrDefault = valueOrDefault;
    }

    // Conversion

#pragma warning disable CS8603 // Possible null reference return.
    public override string ToString()
        => Value.ToString();
#pragma warning restore CS8603 // Possible null reference return.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out bool hasValue, out T value)
    {
        hasValue = HasValue;
        value = ValueOrDefault;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValue(out T value)
    {
        value = ValueOrDefault;
        return HasValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Option<T> ToOption()
        => HasValue ? new Option<T>(true, ValueOrDefault) : default;

    T? IConvertibleTo<T?>.Convert() => Value;
    Option<T> IConvertibleTo<Option<T>>.Convert() => ToOption();

    // Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ApiNullable8<T> other)
        => HasValue
            ? other.HasValue && EqualityComparer<T>.Default.Equals(ValueOrDefault, other.ValueOrDefault)
            : !other.HasValue;
    public override bool Equals(object? obj)
        => obj is ApiNullable8<T> other && Equals(other);
    public override int GetHashCode()
        => HasValue ? ValueOrDefault.GetHashCode() : int.MinValue;

    // Comparison

    public int CompareTo(ApiNullable8<T> other)
        => HasValue
            ? other.HasValue ? Comparer<T>.Default.Compare(ValueOrDefault, other.ValueOrDefault) : 1
            : other.HasValue ? -1 : 0;

    // Operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ApiNullable8<T>(T source) => new(source);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ApiNullable8<T>(T? source) => new(source);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator T?(ApiNullable8<T> source) => source.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ApiNullable8<T> left, ApiNullable8<T> right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ApiNullable8<T> left, ApiNullable8<T> right) => !left.Equals(right);
}
