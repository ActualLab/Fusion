namespace ActualLab.Rpc;

/// <summary>
/// Defines the kind of an RPC method (system, query, command, or other).
/// </summary>
public enum RpcMethodKind
{
    Other = 0,
    System,
    Query,
    Command,
}
