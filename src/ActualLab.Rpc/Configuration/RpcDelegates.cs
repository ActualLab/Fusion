using ActualLab.Interception;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;

namespace ActualLab.Rpc;

// Configuration related
public delegate RpcServiceDef RpcServiceDefBuilder(RpcHub hub, RpcServiceBuilder service);
public delegate RpcMethodDef RpcMethodDefBuilder(RpcServiceDef service, MethodInfo method);
public delegate bool RpcBackendServiceDetector(Type serviceType);
public delegate bool RpcCommandTypeDetector(Type type);
public delegate string RpcServiceScopeResolver(RpcServiceDef serviceDef);
public delegate string RpcHashProvider(ReadOnlyMemory<byte> bytes);

// RpcInboundContext / RpcOutboundContext factories
public delegate RpcInboundContext RpcInboundContextFactory(
    RpcPeer peer, RpcMessage message, CancellationToken cancellationToken);

// Call validation
public delegate Action<RpcInboundCall>? RpcCallValidatorProvider(RpcMethodDef method);
public delegate bool RpcInboundCallFilter(RpcPeer peer, RpcMethodDef method);

// Call routing
public delegate RpcPeerRef RpcCallRouter(RpcMethodDef method, ArgumentList arguments);
public delegate Task RpcRerouteDelayer(CancellationToken cancellationToken);

// Call timeouts
public delegate RpcCallTimeouts RpcCallTimeoutsProvider(RpcMethodDef methodDef);

// RpcPeer management
public delegate RpcPeer RpcPeerFactory(RpcHub hub, RpcPeerRef peerRef);
public delegate void RpcPeerTracker(RpcPeer peer);
public delegate bool RpcPeerTerminalErrorDetector(Exception error);

// Server-side RPC peer management
public delegate Task<RpcConnection> RpcServerConnectionFactory(
    RpcServerPeer peer, Channel<RpcMessage> channel, PropertyBag properties, CancellationToken cancellationToken);
public delegate TimeSpan RpcServerPeerCloseTimeoutProvider(RpcServerPeer peer);

// WebSocket channel
public delegate WebSocketChannel<RpcMessage>.Options RpcWebSocketChannelOptionsProvider(
    RpcPeer peer, PropertyBag properties);

// Call tracing and logging
public delegate RpcCallTracer? RpcCallTracerFactory(RpcMethodDef method);
public delegate RpcCallLogger RpcCallLoggerFactory(RpcPeer peer, RpcCallLoggerFilter filter, ILogger log, LogLevel logLevel);
public delegate bool RpcCallLoggerFilter(RpcPeer peer, RpcCall call);
