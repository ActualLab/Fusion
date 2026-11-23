namespace ActualLab.Serialization;

#pragma warning disable CA2255

/// <summary>
/// Registers Nerdbank.MessagePack as <see cref="SerializerKind.NerdbankMessagePack"/>.
/// Runs automatically when this assembly loads; calling <see cref="Register"/> again is harmless.
/// </summary>
public static class NerdbankMessagePackSerialization
{
    [ModuleInitializer]
    public static void Register()
        => SerializerKind.NerdbankMessagePack.Register(
            () => NerdbankMessagePackByteSerializer.Default,
            () => NerdbankMessagePackByteSerializer.DefaultTypeDecorating);
}
