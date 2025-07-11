using System.Diagnostics.CodeAnalysis;
using MessagePack;

namespace ActualLab.Serialization;

public static class NewtonsoftJsonSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NewtonsoftJsonSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NewtonsoftJsonSerialized<TValue> New<TValue>(string data)
        => new() { Data = data };
}

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class NewtonsoftJsonSerialized<T> : TextSerialized<T>
{
    private static volatile ITextSerializer<T>? _serializer;

    protected override ITextSerializer<T> GetSerializer()
    {
        if (_serializer is { } serializer)
            return serializer;
        lock (StaticLock)
            // ReSharper disable once NonAtomicCompoundOperator
            return _serializer ??= NewtonsoftJsonSerializer.Default.ToTyped<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator NewtonsoftJsonSerialized<T>(T value) => new() { Value = value };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator NewtonsoftJsonSerialized<T>(string data) => new() { Data = data };
}
