using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Conversion;
using MessagePack;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab;

/// <summary>
/// Helper methods related to <see cref="Option{T}"/> type.
/// </summary>
public static class Option
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Option<T> None<T>() => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Option<T> Some<T>(T value) => new(true, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Option<T> FromClass<T>(T? value)
        where T : class
        => ReferenceEquals(value, null)
            ? default
            : new Option<T>(true, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Option<T> FromNullable<T>(T? value)
        where T : struct
        => value.HasValue
            ? new Option<T>(true, value.GetValueOrDefault())
            : default;
}

/// <summary>
/// Describes an optional value ("option" or "maybe"-like type).
/// </summary>
public interface IOption
{
    /// <summary>
    /// Indicates whether an option has <see cref="Value"/>.
    /// </summary>
    public bool HasValue { get; }
    /// <summary>
    /// Retrieves option's value. Throws <see cref="InvalidOperationException"/> in case option doesn't have one.
    /// </summary>
    public object? Value { get; }
}

#pragma warning disable CA1036

[StructLayout(LayoutKind.Sequential)] // Important! Pack = 0 -> Pack = Max(sizeof(bool), sizeof(Value))
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true, SuppressSourceGeneration = true)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
[DebuggerDisplay("{" + nameof(DebugValue) + "}")]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly partial struct Option<T>(bool hasValue, T? valueOrDefault)
    : IOption, ICanBeNone<Option<T>>, IEquatable<Option<T>>, IComparable<Option<T>>, IConvertibleTo<ApiOption<T>>
{
    /// <summary>
    /// Returns an option of type <typeparamref name="T"/> with no value.
    /// </summary>
    public static Option<T> None => default;

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
    public Option<TCast?> CastAs<TCast>()
        where TCast : class
        => HasValue ? new Option<TCast?>(true, ValueOrDefault as TCast) : default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Option<TCast> Cast<TCast>()
    {
        if (!HasValue)
            return default;
        if (ValueOrDefault is TCast value)
            return new Option<TCast>(true, value);
        throw new InvalidCastException();
    }

    public Option<T> ToApiOption() => new(HasValue, ValueOrDefault);
    ApiOption<T> IConvertibleTo<ApiOption<T>>.Convert() => new(HasValue, ValueOrDefault!);

    // Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Option<T> other)
        => HasValue
            ? other.HasValue && EqualityComparer<T>.Default.Equals(ValueOrDefault!, other.ValueOrDefault!)
            : !other.HasValue;
    public override bool Equals(object? obj)
        => obj is Option<T> other && Equals(other);
    public override int GetHashCode()
        => HasValue ? ValueOrDefault?.GetHashCode() ?? 0 : int.MinValue;

    // Comparison

    public int CompareTo(Option<T> other)
        => HasValue
            ? other.HasValue ? Comparer<T>.Default.Compare(ValueOrDefault!, other.ValueOrDefault!) : 1
            : other.HasValue ? -1 : 0;

    // Operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Option<T>(T source) => new(true, source);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Option<T>((bool HasValue, T Value) source) => new(source.HasValue, source.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);

    // Private helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AssertHasValue()
    {
        if (!HasValue)
            throw Errors.OptionIsNone();
    }
}
