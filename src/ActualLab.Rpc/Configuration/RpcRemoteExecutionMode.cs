namespace ActualLab.Rpc;

#pragma warning disable CA2217 // Do not mark enums with FlagsAttribute
#pragma warning disable MA0062 // Enum values should be named correctly

/// <summary>
/// Controls outbound RPC call behavior regarding connection waiting, reconnection, and resending.
/// </summary>
/// <remarks>
/// The resolved mode is stored in <see cref="RpcMethodDef.RemoteExecutionMode"/>,
/// you can use <see cref="RpcMethodAttribute.RemoteExecutionMode"/> to override it.
/// For <see cref="RpcNoWait"/> methods, the mode is always <c>0</c> (no flags).
/// Compute methods must use <see cref="Default"/>.
/// </remarks>
[Flags]
public enum RpcRemoteExecutionMode
{
    /// <summary>
    /// Wait for connection if the peer is disconnected when the call is made.
    /// Without this flag, the call fails immediately if not connected.
    /// </summary>
    AwaitForConnection = 1,

    /// <summary>
    /// Resend the call on reconnection to the same peer.
    /// Without this flag, the call is aborted on disconnect.
    /// </summary>
    AllowReconnect = 2,

    /// <summary>
    /// Resend the call if we reconnect to a different peer (implies <see cref="AllowReconnect"/>).
    /// Without this flag, the call is aborted when the peer identity changes.
    /// </summary>
    AllowResend = 4 | AllowReconnect,

    /// <summary>
    /// Default mode: wait for connection, allow reconnection, and allow resending.
    /// </summary>
    Default = AwaitForConnection | AllowResend,
}
