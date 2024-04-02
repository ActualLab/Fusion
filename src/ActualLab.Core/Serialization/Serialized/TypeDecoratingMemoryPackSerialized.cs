using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Serialization;

public static class TypeDecoratingMemoryPackSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeDecoratingMemoryPackSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static TypeDecoratingMemoryPackSerialized<TValue> New<TValue>(byte[] data)
        => new() { Data = data };
}

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class TypeDecoratingMemoryPackSerialized<T> : ByteSerialized<T>
{
    private static IByteSerializer<T>? _serializer;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected override IByteSerializer<T> GetSerializer()
        => _serializer ??= MemoryPackByteSerializer.DefaultTypeDecorating.ToTyped<T>();
}
