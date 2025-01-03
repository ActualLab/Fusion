using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Api.Internal;
using ActualLab.Conversion;
using ActualLab.Internal;
using MessagePack;

namespace ActualLab.Api;

#pragma warning disable CA1036

public static class ApiOption
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiOption<T> None<T>() => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiOption<T> Some<T>(T value) => new(true, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiOption<T> FromClass<T>(T? value)
        where T : class
        => ReferenceEquals(value, null)
            ? default
            : new ApiOption<T>(true, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ApiOption<T> FromNullable<T>(T? value)
        where T : struct
        => value.HasValue
            ? new ApiOption<T>(true, value.GetValueOrDefault())
            : default;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)] // Important!
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
#if NET8_0_OR_GREATER
[MessagePackObject(true, SuppressSourceGeneration = true)]
#else
[MessagePackFormatter(typeof(ApiOptionMessagePackFormatter<>))]
#endif
[DebuggerDisplay("{" + nameof(DebugValue) + "}")]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial struct ApiOption<T>(bool hasValue, T valueOrDefault)
    : IOption, ICanBeNone<ApiOption<T>>, IEquatable<ApiOption<T>>, IComparable<ApiOption<T>>, IConvertibleTo<Option<T>>
{
    /// <summary>
    /// Returns an option of type <typeparamref name="T"/> with no value.
    /// </summary>
    public static ApiOption<T> None => default;

    /// <inheritdoc />
    [DataMember(Order = 0), MemoryPackOrder(0)]
    public bool HasValue { get; } = hasValue;

    /// <summary>
    /// Retrieves option's value. Returns <code>default(T)</code> in case option doesn't have one.
    /// </summary>
    [DataMember(Order = 1), MemoryPackOrder(1)]
    public T? ValueOrDefault { get; } = valueOrDefault;

    /// <summary>
    /// Retrieves option's value. Throws <see cref="InvalidOperationException"/> in case option doesn't have one.
    /// </summary>
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public T Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { AssertHasValue(); return ValueOrDefault!; }
    }

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public bool IsNone {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !HasValue;
    }

    /// <inheritdoc />
    // ReSharper disable once HeapView.BoxingAllocation
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    object? IOption.Value => Value;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    private string DebugValue => ToString();

    /// <inheritdoc />
    public override string ToString()
        => IsSome(out var v) ? $"Some({v})" : "None";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out bool hasValue, [MaybeNullWhen(false)] out T value)
    {
        hasValue = HasValue;
        value = ValueOrDefault!;
    }

    // Useful methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSome([MaybeNullWhen(false)] out T value)
    {
        value = ValueOrDefault!;
        return HasValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ApiOption<TCast?> CastAs<TCast>()
        where TCast : class
        => HasValue ? new ApiOption<TCast?>(true, ValueOrDefault as TCast) : default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ApiOption<TCast> Cast<TCast>()
    {
        if (!HasValue)
            return default;
        if (ValueOrDefault is TCast value)
            return new ApiOption<TCast>(true, value);
        throw new InvalidCastException();
    }

    public Option<T> ToOption() => new(HasValue, ValueOrDefault);
    Option<T> IConvertibleTo<Option<T>>.Convert() => new(HasValue, ValueOrDefault);

    // Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ApiOption<T> other)
        => HasValue
            ? other.HasValue && EqualityComparer<T>.Default.Equals(ValueOrDefault!, other.ValueOrDefault!)
            : !other.HasValue;
    public override bool Equals(object? obj)
        => obj is ApiOption<T> other && Equals(other);
    public override int GetHashCode()
        => HasValue ? ValueOrDefault?.GetHashCode() ?? 0 : int.MinValue;

    // Comparison

    public int CompareTo(ApiOption<T> other)
        => HasValue
            ? other.HasValue ? Comparer<T>.Default.Compare(ValueOrDefault!, other.ValueOrDefault!) : 1
            : other.HasValue ? -1 : 0;

    // Operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ApiOption<T>(T source) => new(true, source);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ApiOption<T>((bool HasValue, T Value) source) => new(source.HasValue, source.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ApiOption<T>(Option<T> source) => new(source.HasValue, source.ValueOrDefault!);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Option<T>(ApiOption<T> source) => new(source.HasValue, source.ValueOrDefault);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ApiOption<T> left, ApiOption<T> right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ApiOption<T> left, ApiOption<T> right) => !left.Equals(right);

    // Private helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AssertHasValue()
    {
        if (!HasValue)
            throw Errors.ApiOptionIsNone();
    }
}
