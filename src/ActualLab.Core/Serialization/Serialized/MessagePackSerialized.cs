using MessagePack;

namespace ActualLab.Serialization;

public static class MessagePackSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MessagePackSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MessagePackSerialized<TValue> New<TValue>(byte[] data)
        => new() { Data = data };
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class MessagePackSerialized<T> : ByteSerialized<T>
{
    private static volatile IByteSerializer<T>? _serializer;

    protected override IByteSerializer<T> GetSerializer()
    {
        if (_serializer is { } serializer)
            return serializer;
        lock (StaticLock)
            // ReSharper disable once NonAtomicCompoundOperator
            return _serializer ??= MessagePackByteSerializer.Default.ToTyped<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator MessagePackSerialized<T>(T value) => new() { Value = value };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator MessagePackSerialized<T>(byte[] data) => new() { Data = data };
}
