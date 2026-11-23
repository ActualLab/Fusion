using ActualLab.Serialization.Trimming;
using ActualLab.Trimming;

namespace ActualLab.Serialization.Internal;

#pragma warning disable CA2255

/// <summary>
/// Module initializer that registers the MemoryPack formatters of ActualLab types
/// and the <see cref="SerializerKind.MemoryPack"/> serializers.
/// </summary>
internal static class MemoryPackModuleInitializer
{
    static MemoryPackModuleInitializer()
    {
        // Only trimming analysis needs it: KeepSerializable's body never runs
        if (CodeKeeper.AlwaysFalse)
            CodeKeeper.Extension = new MemoryPackCodeKeeperExtension(CodeKeeper.Extension);
    }

    [ModuleInitializer]
    internal static void Touch()
        => MemoryPackSerialization.Register();
}
