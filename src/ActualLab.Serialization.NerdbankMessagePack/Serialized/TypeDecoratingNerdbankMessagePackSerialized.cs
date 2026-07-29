namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="TypeDecoratingNerdbankMessagePackSerialized{T}"/>.
/// </summary>
public static class TypeDecoratingNerdbankMessagePackSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingNerdbankMessagePackSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingNerdbankMessagePackSerialized<TValue> New<TValue>(byte[] data)
        => new() { Data = data };
}

/// <summary>
/// A <see cref="ByteSerialized{T}"/> variant that uses type-decorating Nerdbank.MessagePack serialization.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class TypeDecoratingNerdbankMessagePackSerialized<T> : ByteSerialized<T>
{
    private static IByteSerializer<T>? _serializer;

    protected override IByteSerializer<T> GetSerializer()
    {
        if (_serializer is { } serializer)
            return serializer;
        lock (StaticLock) {
            if (_serializer is { } newSerializer)
                return newSerializer;

            newSerializer = NerdbankMessagePackByteSerializer.DefaultTypeDecorating.ToTyped<T>();
            Volatile.Write(ref _serializer, newSerializer);
            return newSerializer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TypeDecoratingNerdbankMessagePackSerialized<T>(T value) => new() { Value = value };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TypeDecoratingNerdbankMessagePackSerialized<T>(byte[] data) => new() { Data = data };
}
