import { createLogProvider, LogLevel } from '@actuallab/core';

// Per-package log scopes.  Use createLogProvider to get a typed helper:
//   const { debugLog, warnLog } = getLogs('RpcPeer');
// Final scope name is 'rpc.RpcPeer' — the prefix isolates this package's
// scopes from other packages' scopes when overriding levels at runtime.
export type RpcLogScope =
    | 'RpcCallTracker'
    | 'RpcClient'
    | 'RpcClientPeerReconnectDelayer'
    | 'RpcConnection'
    | 'RpcHub'
    | 'RpcInterceptor'
    | 'RpcMessageChannelConnection'
    | 'RpcMethodRegistry'
    | 'RpcOutboundCall'
    | 'RpcPeer'
    | 'RpcPeerStateMonitor'
    | 'RpcRemoteObjectTracker'
    | 'RpcSerialization'
    | 'RpcServiceHost'
    | 'RpcSharedObjectTracker'
    | 'RpcSharedStream'
    | 'RpcStream'
    | 'RpcStreamSender'
    | 'RpcSystemCallHandler'
    | 'RpcSystemCallSender'
    | 'RpcWebSocketClient';

// Per-scope defaults.  All Warn — output goes to the browser console, so the
// out-of-the-box experience must be quiet.  Users opt in to Info-level
// connectivity tracing manually:
//   logLevels.overrideAll('rpc.', LogLevel.Info)
// or for a single scope:
//   logLevels.override('rpc.RpcPeer', LogLevel.Info)
const scopeDefaults: Record<RpcLogScope, LogLevel> = {
    RpcCallTracker: LogLevel.Warn,
    RpcClient: LogLevel.Warn,
    RpcClientPeerReconnectDelayer: LogLevel.Warn,
    RpcConnection: LogLevel.Warn,
    RpcHub: LogLevel.Warn,
    RpcInterceptor: LogLevel.Warn,
    RpcMessageChannelConnection: LogLevel.Warn,
    RpcMethodRegistry: LogLevel.Warn,
    RpcOutboundCall: LogLevel.Warn,
    RpcPeer: LogLevel.Info,
    RpcPeerStateMonitor: LogLevel.Warn,
    RpcRemoteObjectTracker: LogLevel.Warn,
    RpcSerialization: LogLevel.Warn,
    RpcServiceHost: LogLevel.Warn,
    RpcSharedObjectTracker: LogLevel.Warn,
    RpcSharedStream: LogLevel.Warn,
    RpcStream: LogLevel.Warn,
    RpcStreamSender: LogLevel.Warn,
    RpcSystemCallHandler: LogLevel.Warn,
    RpcSystemCallSender: LogLevel.Warn,
    RpcWebSocketClient: LogLevel.Warn,
};

export const getLogs = createLogProvider<RpcLogScope>('rpc.', scopeDefaults);
