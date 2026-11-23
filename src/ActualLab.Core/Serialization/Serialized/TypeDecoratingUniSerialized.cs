using ActualLab.Serialization.Internal;
using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="TypeDecoratingUniSerialized{TSchema,T}"/>.
/// </summary>
public static class TypeDecoratingUniSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingUniSerialized<TSchema, TValue> New<TSchema, TValue>(TValue value = default!)
        where TSchema : TypeSchema
        => new() { Value = value };
}

/// <summary>
/// A type-decorating variant of <see cref="UniSerialized{T}"/>: its nested payload carries the actual type
/// of <see cref="Value"/>, which must be one <typeparamref name="TSchema"/> allows to materialize.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(UniSerializedJsonConverter))]
[MessagePackFormatter(typeof(TypeDecoratingUniSerializedMessagePackFormatter<,>))]
public readonly struct TypeDecoratingUniSerialized<TSchema, T>
    where TSchema : TypeSchema
{
    public T Value { get; init; } = default!;

    public TypeDecoratingUniSerialized()
    { }

    public override string ToString()
        => $"{GetType().GetName()}({Value})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TypeDecoratingUniSerialized<TSchema, T>(T value) => new() { Value = value };
}
