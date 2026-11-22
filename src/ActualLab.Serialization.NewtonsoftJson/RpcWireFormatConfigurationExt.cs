using ActualLab.Serialization;

namespace ActualLab.Rpc;

/// <summary>
/// Extension methods adding Newtonsoft.Json formats to <see cref="RpcWireFormatConfiguration"/>.
/// </summary>
public static class RpcWireFormatConfigurationExt
{
    public static void AddNewtonsoftJson(this RpcWireFormatConfiguration formats)
        => formats.Add(RpcNewtonsoftJsonWireFormats.All);
}
