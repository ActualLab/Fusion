namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Defines the kind of change detected in a remote peer during handshake comparison.
/// </summary>
public enum RpcPeerChangeKind
{
    Unchanged = 0,
    ChangedToVeryFirst = 1,
    Changed = 2,
}
