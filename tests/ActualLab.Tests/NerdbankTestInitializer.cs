#if NET8_0_OR_GREATER

namespace ActualLab.Tests;

#pragma warning disable CA2255
internal static class NerdbankTestInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        SerializationTestExt.UseNerdbankMessagePackSerializer = true;
        RpcNerdbankSerializationFormat.Register();
    }
}

#endif
