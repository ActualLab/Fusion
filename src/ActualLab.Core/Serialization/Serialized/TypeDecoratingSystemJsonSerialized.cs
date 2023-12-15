using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Serialization;

public static class TypeDecoratingSystemJsonSerialized
{
    public static TypeDecoratingSystemJsonSerialized<TValue> New<TValue>() => new();
    public static TypeDecoratingSystemJsonSerialized<TValue> New<TValue>(TValue value) => new() { Value = value };
    public static TypeDecoratingSystemJsonSerialized<TValue> New<TValue>(string data) => new(data);
}

#if !NET5_0
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
#endif
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public partial class TypeDecoratingSystemJsonSerialized<T> : TextSerialized<T>
{
    [ThreadStatic] private static ITextSerializer<T>? _serializer;

    public TypeDecoratingSystemJsonSerialized() { }

    [MemoryPackConstructor]
    public TypeDecoratingSystemJsonSerialized(string data)
        : base(data) { }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected override ITextSerializer<T> GetSerializer()
        => _serializer ??= new TypeDecoratingTextSerializer(SystemJsonSerializer.Default).ToTyped<T>();
}
