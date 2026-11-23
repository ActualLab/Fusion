using ActualLab.Serialization;

namespace ActualLab.Rpc;

/// <summary>
/// <see cref="RpcWireFormatConfiguration"/> extensions adding the Nerdbank.MessagePack formats.
/// </summary>
public static class RpcWireFormatConfigurationExt
{
    // Adds "nmsgpack6" and "nmsgpack6c"; call AddLZ4Variants to add their LZ4 variants
    public static void AddNerdbankMessagePack(this RpcWireFormatConfiguration formats)
        => formats.Add(RpcNerdbankWireFormats.All);
}
