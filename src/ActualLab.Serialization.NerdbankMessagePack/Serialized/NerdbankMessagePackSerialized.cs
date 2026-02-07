namespace ActualLab.Serialization;

/// <summary>
/// Factory methods for <see cref="NerdbankMessagePackSerialized{T}"/>.
/// </summary>
public static class NerdbankMessagePackSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NerdbankMessagePackSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NerdbankMessagePackSerialized<TValue> New<TValue>(byte[] data)
        => new() { Data = data };
}

/// <summary>
/// A <see cref="ByteSerialized{T}"/> variant that uses <see cref="NerdbankMessagePackByteSerializer"/> for serialization.
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class NerdbankMessagePackSerialized<T> : ByteSerialized<T>
{
    private static volatile IByteSerializer<T>? _serializer;

    protected override IByteSerializer<T> GetSerializer()
    {
        if (_serializer is { } serializer)
            return serializer;
        lock (StaticLock)
            // ReSharper disable once NonAtomicCompoundOperator
            return _serializer ??= NerdbankMessagePackByteSerializer.Default.ToTyped<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator NerdbankMessagePackSerialized<T>(T value) => new() { Value = value };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator NerdbankMessagePackSerialized<T>(byte[] data) => new() { Data = data };
}
