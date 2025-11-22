namespace ActualLab.Rpc.Infrastructure;

public enum RpcRoutingMode
{
    /// <summary>
    /// <see cref="RpcMethodDef.RouteOutboundCall"/> is used to route such calls.
    /// </summary>
    Outbound = 0,
    /// <summary>
    /// <see cref="RpcMethodDef.RouteInboundCall"/> is used to route such calls.
    /// It resolves to local peer for any service other than <see cref="RpcServiceMode.Distributed"/>;
    /// as for the distributed services, it throws <see cref="RpcRerouteException"/> in case
    /// the resolved peer is not local.
    /// </summary>
    Inbound,
    /// <summary>
    /// The call is pre-routed, so the peer is set explicitly.
    /// </summary>
    Prerouted,
}
