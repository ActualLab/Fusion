using ActualLab.Serialization.Internal;
using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="UniSerialized{T}"/>.
/// </summary>
public static class UniSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UniSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };
}

/// <summary>
/// A wrapper serializing its <see cref="Value"/> as a nested payload produced by the serializer that serializes
/// the wrapper itself (see <see cref="ByteSerializer.Current"/>). Every serializer's converter defines the envelope,
/// e.g. <c>{"json":"..."}</c> for System.Text.Json or a single-item array for MessagePack.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(UniSerializedJsonConverter))]
[MessagePackFormatter(typeof(UniSerializedMessagePackFormatter<>))]
public readonly struct UniSerialized<T>
{
    public T Value { get; init; } = default!;

    public UniSerialized()
    { }

    public override string ToString()
        => $"{GetType().GetName()}({Value})";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator UniSerialized<T>(T value) => new() { Value = value };
}
