using ActualLab.OS;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public class RpcDiagnosticsOptions
{
    public static RpcDiagnosticsOptions Default { get; set; } = new();

    protected static readonly string KeepAliveMethodName = $"{nameof(IRpcSystemCalls.KeepAlive)}:1";

    public virtual RpcCallTracer? CreateCallTracer(RpcMethodDef method)
        => RuntimeInfo.IsServer
            ? new RpcDefaultCallTracer(method)
            : null;


    public virtual RpcCallLogger CreateCallLogger(RpcPeer peer, ILogger? log, LogLevel logLevel)
        => new(peer, log, logLevel) {
            Filter = static (peer, call) => {
                var methodDef = call.MethodDef;
                return !(methodDef.IsSystem
                    && string.Equals(methodDef.Name, KeepAliveMethodName, StringComparison.Ordinal));
            },
        };
}
