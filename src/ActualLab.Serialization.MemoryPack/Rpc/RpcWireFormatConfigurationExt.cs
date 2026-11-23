using ActualLab.Serialization;

namespace ActualLab.Rpc;

/// <summary>
/// <see cref="RpcWireFormatConfiguration"/> extensions adding the MemoryPack formats.
/// </summary>
public static class RpcWireFormatConfigurationExt
{
    // Adds "mempack5", "mempack5c", "mempack6" and "mempack6c"; AddLZ4Variants adds their LZ4 variants
    public static void AddMemoryPack(this RpcWireFormatConfiguration formats)
        => formats.Add(RpcMemoryPackWireFormats.All);
}
