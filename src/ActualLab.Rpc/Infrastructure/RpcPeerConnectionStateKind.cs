namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable CA1027 // Add [Flags] attribute to enum

public enum RpcPeerConnectionStateKind
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Terminal = 0x10,
}
