using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="TypeDecoratingMemoryPackSerialized{TSchema,T}"/>.
/// </summary>
public static class TypeDecoratingMemoryPackSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingMemoryPackSerialized<TSchema, TValue> New<TSchema, TValue>(TValue value = default!)
        where TSchema : TypeSchema, new()
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingMemoryPackSerialized<TSchema, TValue> New<TSchema, TValue>(byte[] data)
        where TSchema : TypeSchema, new()
        => new() { Data = data };
}

/// <summary>
/// A <see cref="ByteSerialized{T}"/> variant that uses type-decorating MemoryPack serialization.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class TypeDecoratingMemoryPackSerialized<TSchema, T> : ByteSerialized<T>
    where TSchema : TypeSchema, new()
{
    private static IByteSerializer<T>? _serializer;

    protected override IByteSerializer<T> GetSerializer()
    {
        if (_serializer is { } serializer)
            return serializer;
        lock (StaticLock) {
            if (_serializer is { } newSerializer)
                return newSerializer;

            newSerializer = TypeSchema<TSchema>.GetTypeDecoratingSerializer(SerializerKind.MemoryPack).ToTyped<T>();
            Volatile.Write(ref _serializer, newSerializer);
            return newSerializer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TypeDecoratingMemoryPackSerialized<TSchema, T>(T value) => new() { Value = value };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TypeDecoratingMemoryPackSerialized<TSchema, T>(byte[] data) => new() { Data = data };
}
