using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="MessagePackSerialized{T}"/>.
/// </summary>
public static class MessagePackSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MessagePackSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MessagePackSerialized<TValue> New<TValue>(byte[] data)
        => new() { Data = data };
}

/// <summary>
/// A <see cref="ByteSerialized{T}"/> variant that uses <see cref="MessagePackByteSerializer"/> for serialization.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class MessagePackSerialized<T> : ByteSerialized<T>
{
    private static IByteSerializer<T>? _serializer;

    protected override IByteSerializer<T> GetSerializer()
    {
        if (_serializer is { } serializer)
            return serializer;
        lock (StaticLock) {
            if (_serializer is { } newSerializer)
                return newSerializer;

            newSerializer = MessagePackByteSerializer.Default.ToTyped<T>();
            Volatile.Write(ref _serializer, newSerializer);
            return newSerializer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator MessagePackSerialized<T>(T value) => new() { Value = value };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator MessagePackSerialized<T>(byte[] data) => new() { Data = data };
}
