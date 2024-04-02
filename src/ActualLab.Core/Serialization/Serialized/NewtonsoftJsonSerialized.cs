using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Serialization;

public static class NewtonsoftJsonSerialized
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NewtonsoftJsonSerialized<TValue> New<TValue>(TValue value = default!)
        => new() { Value = value };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static NewtonsoftJsonSerialized<TValue> New<TValue>(string data)
        => new() { Data = data };
}

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class NewtonsoftJsonSerialized<T> : TextSerialized<T>
{
    private static ITextSerializer<T>? _serializer;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected override ITextSerializer<T> GetSerializer()
        => _serializer ??= NewtonsoftJsonSerializer.Default.ToTyped<T>();
}
