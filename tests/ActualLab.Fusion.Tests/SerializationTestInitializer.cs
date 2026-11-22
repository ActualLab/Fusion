namespace ActualLab.Fusion.Tests;

#pragma warning disable CA2255

internal static class SerializationTestInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
        => ActualLab.Tests.SerializationTestInitializer.Initialize();
}
