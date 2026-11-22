namespace ActualLab.Serialization;

#pragma warning disable CA2255

/// <summary>
/// Registers Newtonsoft.Json as <see cref="SerializerKind.NewtonsoftJson"/>.
/// Runs automatically when this assembly loads; calling <see cref="Register"/> again is harmless.
/// </summary>
public static class NewtonsoftJsonSerialization
{
    [ModuleInitializer]
    public static void Register()
        => SerializerKind.NewtonsoftJson.Register(
            () => NewtonsoftJsonSerializer.Default,
            () => NewtonsoftJsonSerializer.DefaultTypeDecorating);
}
