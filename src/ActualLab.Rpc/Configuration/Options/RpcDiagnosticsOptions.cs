using ActualLab.OS;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

/// <summary>
/// Configuration options for RPC diagnostics, including call tracing and logging factories.
/// </summary>
public record RpcDiagnosticsOptions
{
    public static RpcDiagnosticsOptions Default { get; set; } = new();
    protected static readonly string KeepAliveMethodName = $"{nameof(IRpcSystemCalls.KeepAlive)}:1";

    // Delegate options
    public Func<RpcPeer, TimeSpan> OpenCallMetricsPeriodProvider { get; init; }
    public Func<RpcMethodDef, RpcCallTracer?> CallTracerFactory { get; init; }
    public Func<RpcPeer, ILogger?, LogLevel, RpcCallLogger> CallLoggerFactory { get; init; }

    // ReSharper disable once ConvertConstructorToMemberInitializers
    public RpcDiagnosticsOptions()
    {
        OpenCallMetricsPeriodProvider = DefaultOpenCallMetricsPeriodProvider;
        CallTracerFactory = DefaultCallTracerFactory;
        CallLoggerFactory = DefaultCallLoggerFactory;
    }

    // Protected methods

    protected static TimeSpan DefaultOpenCallMetricsPeriodProvider(RpcPeer peer)
        => TimeSpan.FromMinutes(peer.Ref.IsServer ? 5 : 1);

    protected static RpcCallTracer? DefaultCallTracerFactory(RpcMethodDef method)
        => RuntimeInfo.IsServer
            ? new RpcDefaultCallTracer(method)
            : null;

    protected static RpcCallLogger DefaultCallLoggerFactory(RpcPeer peer, ILogger? log, LogLevel logLevel)
    {
        return new RpcCallLogger(peer, log, logLevel) {
            Filter = static (peer, call) => {
                var methodDef = call.MethodDef;
                return !(methodDef.IsSystem
                    && string.Equals(methodDef.Name, KeepAliveMethodName, StringComparison.Ordinal));
            },
        };
    }
}
