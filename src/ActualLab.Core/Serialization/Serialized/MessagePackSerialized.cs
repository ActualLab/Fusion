using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Serialization;

public static class MessagePackSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MessagePackSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static MessagePackSerialized<TValue> New<TValue>(byte[] data)
        => new() { Data = data };
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class MessagePackSerialized<T> : ByteSerialized<T>
{
    private static IByteSerializer<T>? _serializer;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected override IByteSerializer<T> GetSerializer()
        => _serializer ??= MessagePackByteSerializer.Default.ToTyped<T>();
}
