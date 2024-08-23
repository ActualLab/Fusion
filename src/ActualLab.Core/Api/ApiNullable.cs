using System.Diagnostics;
using ActualLab.Conversion;

namespace ActualLab.Api;

#pragma warning disable CA1036
#pragma warning disable CS0618 // Type or member is obsolete

public static class ApiNullable
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiNullable<T> Null<T>() where T : struct => default;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiNullable<T> Value<T>(T value) where T : struct => new(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiNullable<T> From<T>(T? value) where T : struct => new(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiNullable<T> From<T>(Option<T> value)
        where T : struct
        => value.IsSome(out var v) ? new(true, v) : default;
}

/// <summary>
/// Like <see cref="Nullable{T}"/>, but with 1-byte HasValue.
/// </summary>
/// <typeparam name="T">The type of <see cref="Value"/>.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DebuggerDisplay("{" + nameof(DebugValue) + "}")]
public readonly partial struct ApiNullable<T>
    : IEquatable<ApiNullable<T>>, IComparable<ApiNullable<T>>,
        IConvertibleTo<T?>, IConvertibleTo<Option<T>>
    where T : struct
{
    public static readonly ApiNullable<T> Null;

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    private string DebugValue => ToString();

    [DataMember(Order = 0), MemoryPackOrder(0), JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public bool HasValue { get; }

    [DataMember(Order = 1), MemoryPackOrder(1), JsonIgnore, Newtonsoft.Json.JsonIgnore]
    public T ValueOrDefault { get; }

    [JsonInclude, Newtonsoft.Json.JsonProperty, MemoryPackIgnore]
    public T? Value => HasValue ? ValueOrDefault : null;

    // Constructors

    [JsonConstructor, Newtonsoft.Json.JsonConstructor]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ApiNullable(T? value)
    {
        HasValue = value.HasValue;
        ValueOrDefault = value.GetValueOrDefault();
    }

    [MemoryPackConstructor]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once ConvertToPrimaryConstructor
    public ApiNullable(bool hasValue, T valueOrDefault)
    {
        HasValue = hasValue;
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
    public bool Equals(ApiNullable<T> other)
        => HasValue
            ? other.HasValue && EqualityComparer<T>.Default.Equals(ValueOrDefault, other.ValueOrDefault)
            : !other.HasValue;
    public override bool Equals(object? obj)
        => obj is ApiNullable<T> other && Equals(other);
    public override int GetHashCode()
        => HasValue ? ValueOrDefault.GetHashCode() : int.MinValue;

    // Comparison

    public int CompareTo(ApiNullable<T> other)
        => HasValue
            ? other.HasValue ? Comparer<T>.Default.Compare(ValueOrDefault, other.ValueOrDefault) : 1
            : other.HasValue ? -1 : 0;

    // Operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ApiNullable<T>(T source) => new(source);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ApiNullable<T>(T? source) => new(source);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator T?(ApiNullable<T> source) => source.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ApiNullable<T> left, ApiNullable<T> right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ApiNullable<T> left, ApiNullable<T> right) => !left.Equals(right);
}
