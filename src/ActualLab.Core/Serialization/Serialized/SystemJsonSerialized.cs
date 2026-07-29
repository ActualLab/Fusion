using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="SystemJsonSerialized{T}"/>.
/// </summary>
public static class SystemJsonSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SystemJsonSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SystemJsonSerialized<TValue> New<TValue>(string data)
        => new() { Data = data };
}

/// <summary>
/// A <see cref="TextSerialized{T}"/> variant that uses <see cref="SystemJsonSerializer"/> for serialization.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class SystemJsonSerialized<T> : TextSerialized<T>
{
    private static ITextSerializer<T>? _serializer;

    protected override ITextSerializer<T> GetSerializer()
    {
        if (_serializer is { } serializer)
            return serializer;
        lock (StaticLock) {
            if (_serializer is { } newSerializer)
                return newSerializer;

            newSerializer = SystemJsonSerializer.Default.ToTyped<T>();
            Volatile.Write(ref _serializer, newSerializer);
            return newSerializer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SystemJsonSerialized<T>(T value) => new() { Value = value };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SystemJsonSerialized<T>(string data) => new() { Data = data };
}
