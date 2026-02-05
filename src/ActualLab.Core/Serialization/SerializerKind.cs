namespace ActualLab.Serialization;

/// <summary>
/// Defines the available serializer implementations.
/// </summary>
public enum SerializerKind
{
    None = 0,
    MemoryPack,
    MessagePack,
    SystemJson,
    NewtonsoftJson,
}

/// <summary>
/// Extension methods for <see cref="SerializerKind"/> to resolve default serializer instances.
/// </summary>
public static class SerializerKindExt
{
    public static IByteSerializer GetDefaultSerializer(this SerializerKind serializerKind)
        => serializerKind switch {
            SerializerKind.MemoryPack => MemoryPackByteSerializer.Default,
            SerializerKind.MessagePack => MessagePackByteSerializer.Default,
            SerializerKind.SystemJson => SystemJsonSerializer.Default,
            SerializerKind.NewtonsoftJson => NewtonsoftJsonSerializer.Default,
            _ => throw new ArgumentOutOfRangeException(nameof(serializerKind)),
        };

    public static IByteSerializer GetDefaultTypeDecoratingSerializer(this SerializerKind serializerKind)
        => serializerKind switch {
            SerializerKind.MemoryPack => MemoryPackByteSerializer.DefaultTypeDecorating,
            SerializerKind.MessagePack => MessagePackByteSerializer.DefaultTypeDecorating,
            SerializerKind.SystemJson => SystemJsonSerializer.DefaultTypeDecorating,
            SerializerKind.NewtonsoftJson => NewtonsoftJsonSerializer.DefaultTypeDecorating,
            _ => throw new ArgumentOutOfRangeException(nameof(serializerKind)),
        };
}
