using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="TypeDecoratingSystemJsonSerialized{TSchema,T}"/>.
/// </summary>
public static class TypeDecoratingSystemJsonSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingSystemJsonSerialized<TSchema, TValue> New<TSchema, TValue>(TValue value = default!)
        where TSchema : TypeSchema, new()
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingSystemJsonSerialized<TSchema, TValue> New<TSchema, TValue>(string data)
        where TSchema : TypeSchema, new()
        => new() { Data = data };
}

/// <summary>
/// A <see cref="TextSerialized{T}"/> variant that uses type-decorating System.Text.Json serialization.
/// </summary>
#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class TypeDecoratingSystemJsonSerialized<TSchema, T> : TextSerialized<T>
    where TSchema : TypeSchema, new()
{
    private static ITextSerializer<T>? _serializer;

    protected override ITextSerializer<T> GetSerializer()
    {
        if (_serializer is { } serializer)
            return serializer;
        lock (StaticLock) {
            if (_serializer is { } newSerializer)
                return newSerializer;

            newSerializer = ((ITextSerializer)TypeSchema<TSchema>
                .GetTypeDecoratingSerializer(SerializerKind.SystemJson)).ToTyped<T>();
            Volatile.Write(ref _serializer, newSerializer);
            return newSerializer;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TypeDecoratingSystemJsonSerialized<TSchema, T>(T value) => new() { Value = value };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TypeDecoratingSystemJsonSerialized<TSchema, T>(string data) => new() { Data = data };
}
