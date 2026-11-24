#if !NETSTANDARD2_0
using ActualLab.Serialization;
using ActualLab.Serialization.Internal;
using MemoryPack;

namespace ActualLab.Fusion.Extensions.Internal;

#pragma warning disable CA2255

internal static class FusionExtMemoryPackModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        MemoryPackSerialization.RegisterGenericType(typeof(PageRef<>), typeof(PageRefMemoryPackFormatter<>));
        MemoryPackFormatterProvider.Register(new UserIdentityMemoryPackFormatter());
        MemoryPackFormatterProvider.Register(new UserMemoryPackFormatter());
        MemoryPackFormatterProvider.Register(new SessionAuthInfoMemoryPackFormatter());
        MemoryPackFormatterProvider.Register(new SessionInfoMemoryPackFormatter());
        MemoryPackFormatterProvider.Register(new AuthEditUserMemoryPackFormatter());
        MemoryPackFormatterProvider.Register(new AuthSignOutMemoryPackFormatter());
    }
}
#endif
