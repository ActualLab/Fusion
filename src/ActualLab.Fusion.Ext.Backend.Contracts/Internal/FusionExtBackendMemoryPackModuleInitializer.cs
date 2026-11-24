#if !NETSTANDARD2_0
using ActualLab.Serialization.Internal;
using MemoryPack;

namespace ActualLab.Fusion.Extensions.Internal;

#pragma warning disable CA2255

internal static class FusionExtBackendMemoryPackModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        MemoryPackFormatterProvider.Register(new AuthBackendSetSessionOptionsMemoryPackFormatter());
        MemoryPackFormatterProvider.Register(new AuthBackendSetupSessionMemoryPackFormatter());
        MemoryPackFormatterProvider.Register(new AuthBackendSignInMemoryPackFormatter());
    }
}
#endif
