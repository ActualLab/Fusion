using ActualLab.Interception.Serialization;
using ActualLab.Rpc;
using ActualLab.Rpc.Serialization;

// ReSharper disable InconsistentNaming

namespace ActualLab.Serialization;

/// <summary>
/// Newtonsoft.Json-based <see cref="RpcWireFormat"/>s.
/// Clients can't select them unless the server allows it, see
/// <see cref="RpcDefaultWireFormats.BackendOnlyFormatKeys"/>.
/// </summary>
public static class RpcNewtonsoftJsonWireFormats
{
    public static readonly RpcWireFormat NewtonsoftJsonV5 = new("njson5",
        () => new TextArgumentListSerializer(NewtonsoftJsonSerializer.Default),
        peer => new RpcTextMessageSerializerV3(peer),
        DataFormat.Text);
    // No polymorphism
    public static readonly RpcWireFormat NewtonsoftJsonV5NP = new("njson5np",
        () => new TextArgumentListSerializerNP(NewtonsoftJsonSerializer.Default),
        peer => new RpcTextMessageSerializerV3(peer),
        DataFormat.Text);

    public static readonly ImmutableList<RpcWireFormat> All = ImmutableList.Create(
        NewtonsoftJsonV5, NewtonsoftJsonV5NP);
}
