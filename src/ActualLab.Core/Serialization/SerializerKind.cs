using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

/// <summary>
/// Defines the available serializer implementations.
/// MemoryPack, NewtonsoftJson and NerdbankMessagePack are provided by separate packages, which register them
/// on startup; CustomN kinds are free for apps to register their own serializers.
/// </summary>
public enum SerializerKind
{
    None = 0,
    MessagePack = 1,
    SystemJson = 2,
    MemoryPack = 3,
    NewtonsoftJson = 4,
    NerdbankMessagePack = 5,
    Custom0 = 0x10,
    Custom1 = 0x11,
    Custom2 = 0x12,
    Custom3 = 0x13,
    Custom4 = 0x14,
    Custom5 = 0x15,
    Custom6 = 0x16,
    Custom7 = 0x17,
}

/// <summary>
/// Extension methods for <see cref="SerializerKind"/> to resolve default serializer instances.
/// </summary>
public static class SerializerKindExt
{
    // Indexed by the SerializerKind value
    internal const int MaxKindCount = 32;

    private static readonly Registration?[] Registrations = new Registration?[MaxKindCount];

    // Factories rather than instances: the registered serializers' Default properties are settable
    public static void Register(
        this SerializerKind serializerKind,
        Func<IByteSerializer> defaultSerializerFactory,
        Func<IByteSerializer> defaultTypeDecoratingSerializerFactory)
    {
        if (serializerKind is SerializerKind.None or SerializerKind.MessagePack or SerializerKind.SystemJson
            || (uint)serializerKind >= MaxKindCount)
            throw new ArgumentOutOfRangeException(nameof(serializerKind));

        var registration = new Registration(defaultSerializerFactory, defaultTypeDecoratingSerializerFactory);
        // Release, so the registration is fully constructed before it's visible
        Volatile.Write(ref Registrations[(int)serializerKind], registration);
    }

    public static bool IsAvailable(this SerializerKind serializerKind)
        => serializerKind is SerializerKind.MessagePack or SerializerKind.SystemJson
            || ((uint)serializerKind < MaxKindCount && Registrations[(int)serializerKind] is not null);

    public static IByteSerializer GetDefaultSerializer(this SerializerKind serializerKind)
        => serializerKind switch {
            SerializerKind.MessagePack => MessagePackByteSerializer.Default,
            SerializerKind.SystemJson => SystemJsonSerializer.Default,
            SerializerKind.None => throw new ArgumentOutOfRangeException(nameof(serializerKind)),
            _ => GetRegistration(serializerKind).DefaultSerializerFactory.Invoke(),
        };

    public static IByteSerializer GetDefaultTypeDecoratingSerializer(this SerializerKind serializerKind)
        => serializerKind switch {
            SerializerKind.MessagePack => MessagePackByteSerializer.DefaultTypeDecorating,
            SerializerKind.SystemJson => SystemJsonSerializer.DefaultTypeDecorating,
            SerializerKind.None => throw new ArgumentOutOfRangeException(nameof(serializerKind)),
            _ => GetRegistration(serializerKind).DefaultTypeDecoratingSerializerFactory.Invoke(),
        };

    // Private methods

    private static Registration GetRegistration(SerializerKind serializerKind)
    {
        if ((uint)serializerKind >= MaxKindCount)
            throw new ArgumentOutOfRangeException(nameof(serializerKind));

        return Registrations[(int)serializerKind] ?? throw Errors.SerializerKindIsNotRegistered(serializerKind);
    }

    // Nested types

    private sealed record Registration(
        Func<IByteSerializer> DefaultSerializerFactory,
        Func<IByteSerializer> DefaultTypeDecoratingSerializerFactory);
}
