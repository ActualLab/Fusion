// .NET counterpart:
//   RpcHub (141 lines) — a sealed ProcessorBase (IAsyncDisposable) that owns:
//     DI-resolved configuration (RpcRegistryOptions, PeerOptions, InboundCallOptions,
//     OutboundCallOptions, DiagnosticsOptions), SerializationFormats, Middlewares,
//     ClientPeerReconnectDelayer, Limits, SystemClock.
//     Manages peers via ConcurrentDictionary<RpcPeerRef, RpcPeer> with GetPeer()
//     that lazily creates + starts peers.  Also has GetClient<T>() / GetServer<T>()
//     that look up services in ServiceRegistry.
//
// Omitted from .NET:
//   - DI / IServiceProvider integration — .NET resolves all options, serializers,
//     middleware, delayers, etc. from DI.  TS constructs everything directly.
//   - RpcServiceRegistry — .NET builds a full registry of all RPC services at
//     startup via reflection + DI, mapping Type → RpcServiceDef.  TS uses explicit
//     addService() / addClient() calls.
//   - GetClient<T>() / GetServer<T>() — .NET resolves typed client/server proxies
//     via the ServiceRegistry + DI.  TS uses addClient<T>(peer, def).
//   - RpcPeerRef-keyed peer dictionary — .NET peers are keyed by RpcPeerRef (rich
//     value object encoding route, version, serialization format).  TS uses string
//     refs (URL for clients, "server://{uuid}" for servers).
//   - Lazy peer creation + Start() — .NET's GetPeer auto-creates and starts a
//     peer's reconnection loop.  TS requires explicit addPeer() + run().
//   - DisposeAsync — .NET disposes all peers on hub shutdown.  TS has close().
//   - Route change detection (RouteState.WhenChanged → Dispose peer) — .NET
//     watches for load-balancer route changes and auto-disposes stale peers.
//     TS has no routing layer.
//   - Middlewares (IRpcMiddleware[]) — ordered middleware pipeline for inbound
//     calls.  TS dispatches directly to service implementations.
//   - Limits (RpcLimits) — configurable limits for call timeout, handshake
//     timeout, max message size, etc.  TS uses hardcoded values.
//   - RpcConfiguration / Configuration.Freeze() — frozen config snapshot.
//   - HostId / SystemClock — infrastructure services.
//   - DefaultPeer / LoopbackPeer / LocalPeer / NonePeer — cached well-known peers.

import { getLogs } from './logging.js';
import {
    RpcClientPeer,
    RpcServerPeer,
    type RpcPeer,
    type RpcCallOptions,
} from './rpc-peer.js';
import { RpcClientPeerReconnectDelayer } from './rpc-client-peer-reconnect-delayer.js';
import { RpcServiceHost, type RpcServiceImpl } from './rpc-service-host.js';
import type { RpcServiceDef, RpcMethodDef } from './rpc-service-def.js';
import { wireMethodName, RpcType, RpcRemoteExecutionMode } from './rpc-service-def.js';
import { getServiceMeta, getMethodsMeta, type AnyConstructor } from './rpc-decorators.js';
import { RpcSystemCallSender } from './rpc-system-call-sender.js';
import { RpcSystemCallHandler } from './rpc-system-call-handler.js';
import { RpcStream, parseStreamRef, resolveStreamRefs } from './rpc-stream.js';
import { RpcMethodRegistry } from './rpc-method-registry.js';
import { RpcSystemCalls } from './rpc-message.js';

const { warnLog } = getLogs('RpcHub');

/** Factory signature shared by all peer lookup/creation methods. Serialization
 *  format is encoded in the URL via `?f=...` (see {@link RpcPeerRefBuilder}),
 *  so the factory doesn't need an extra parameter for it. `getClientPeer` /
 *  `getServerPeer` cast the result, so the factory returns RpcPeer. */
export type RpcPeerFactory = (hub: RpcHub, ref: string) => RpcPeer;

/** Central RPC coordinator — manages peers, services, and configuration. */
export class RpcHub {
    readonly hubId: string;
    readonly peers = new Map<string, RpcPeer>();
    readonly serviceHost: RpcServiceHost;
    readonly systemCallSender = new RpcSystemCallSender();
    systemCallHandler: RpcSystemCallHandler = new RpcSystemCallHandler();

    /** Method registry for compact format hash ↔ name resolution.
     *  Created lazily, populated by addService/addClient. */
    readonly registry = new RpcMethodRegistry();

    /** Shared reconnect delayer used by every client peer in this hub. Swap in
     *  a custom subclass (e.g. app-level signal-gated) before peers start. */
    reconnectDelayer: RpcClientPeerReconnectDelayer = new RpcClientPeerReconnectDelayer();

    /** URL used by {@link defaultPeer} to resolve / create the default client
     *  peer. Must be set before the first {@link defaultPeer} access. */
    defaultPeerUrl: string | undefined;
    /** Factory used by {@link defaultPeer} when the peer needs to be created.
     *  If left undefined, `getPeer`'s built-in default (RpcClientPeer with
     *  format derived from URL or resolver default) is used. */
    defaultPeerFactory: RpcPeerFactory | undefined;

    constructor(hubId?: string) {
        this.hubId = hubId ?? crypto.randomUUID();
        this.serviceHost = new RpcServiceHost();
        this.systemCallSender.registry = this.registry;
        // Pre-register system call method names for compact format hash resolution
        for (const methodName of Object.values(RpcSystemCalls)) {
            this.registry.register(methodName);
        }
    }

    /** Shortcut for the hub's default client peer. Fails if
     *  {@link defaultPeerUrl} is not set. */
    get defaultPeer(): RpcClientPeer {
        if (this.defaultPeerUrl === undefined)
            throw new Error('RpcHub.defaultPeerUrl is not set.');
        return this.getClientPeer(this.defaultPeerUrl, this.defaultPeerFactory);
    }

    /** Register a peer under its ref. If a different peer is already registered
     *  at the same ref, close it first — the hub guarantees at most one live
     *  peer per ref, and no peer exists outside the hub. */
    addPeer(peer: RpcPeer): void {
        const existing = this.peers.get(peer.ref);
        if (existing !== undefined && existing !== peer)
            existing.close(); // close() removes itself from this.peers
        this.peers.set(peer.ref, peer);
    }

    removePeer(ref: string): void {
        const peer = this.peers.get(ref);
        if (peer !== undefined) {
            peer.close();
            this.peers.delete(ref);
            // Mirrors RpcHub.cs:137 — "peer is removed from RpcHub".
            warnLog?.log(`'${ref}': peer is removed from RpcHub`);
        }
    }

    /** Get or create a peer by ref. If the peer does not exist:
     *   - with `factory` provided: it is constructed via `factory(this, ref)`;
     *   - otherwise: server peers (ref starts with `server://`) get a plain
     *     RpcServerPeer, anything else gets a default-configured RpcClientPeer.
     *  The created peer is always registered in this hub. */
    getPeer(ref: string, factory?: RpcPeerFactory): RpcPeer {
        const existing = this.peers.get(ref);
        if (existing) return existing;

        const peer = factory
            ? factory(this, ref)
            : ref.startsWith('server://')
                ? new RpcServerPeer(this, ref)
                : new RpcClientPeer(this, ref);
        this.addPeer(peer);
        return peer;
    }

    /** Get or create a client peer for the given URL. See {@link getPeer} for
     *  the factory semantics. */
    getClientPeer(ref: string, factory?: RpcPeerFactory): RpcClientPeer {
        return this.getPeer(ref, factory) as RpcClientPeer;
    }

    /** Get or create a server peer for the given ref. See {@link getPeer} for
     *  the factory semantics. */
    getServerPeer(ref: string, factory?: RpcPeerFactory): RpcServerPeer {
        return this.getPeer(ref, factory) as RpcServerPeer;
    }

    /** Register a service with optional server method wrapping for custom call types. */
    addService(
        defOrContract: RpcServiceDef | (AnyConstructor),
        impl: RpcServiceImpl
    ): void {
        const def = this._resolveServiceDef(defOrContract);
        const wrappedImpl: RpcServiceImpl = {};
        for (const methodDef of def.methods.values()) {
            const fn = impl[methodDef.name];
            if (!fn) continue;
            wrappedImpl[methodDef.name] =
                methodDef.callTypeId !== 0
                    ? this._wrapServerMethod(methodDef, fn, impl as object)
                    : fn;
        }
        this.serviceHost.register(def, wrappedImpl);
        this.registry.registerService(def.name, def.methods);
    }

    /** Create a typed client proxy for a service on a remote peer. */
    // eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- T is used for caller-specified proxy type
    addClient<T extends object>(
        peer: RpcPeer,
        defOrContract: RpcServiceDef | (AnyConstructor)
    ): T {
        const def = this._resolveServiceDef(defOrContract);
        this.registry.registerService(def.name, def.methods);

        type RpcClientFn = (...args: unknown[]) => unknown;

        // Group methods by clean name, indexed by argCount for overload resolution
        const overloads = new Map<string, Map<number, RpcClientFn>>();
        for (const methodDef of def.methods.values()) {
            let byArgCount = overloads.get(methodDef.name);
            if (!byArgCount) {
                byArgCount = new Map<number, RpcClientFn>();
                overloads.set(methodDef.name, byArgCount);
            }
            byArgCount.set(
                methodDef.argCount,
                this._createClientMethod(peer, methodDef)
            );
        }

        // Build final proxy methods — single overload: use directly; multiple: resolve by args.length
        const methods = new Map<string, RpcClientFn>();
        for (const [name, byArgCount] of overloads) {
            if (byArgCount.size === 1) {
                const [[, singleFn]] = byArgCount;
                methods.set(name, singleFn);
            } else {
                methods.set(name, (...args: unknown[]) => {
                    const fn = byArgCount.get(args.length);
                    if (!fn)
                        throw new Error(
                            `No overload of ${name} accepts ${args.length} arguments`
                        );
                    return fn(...args);
                });
            }
        }

        return new Proxy({} as T, {
            get: (_target, prop) =>
                typeof prop === 'string' ? methods.get(prop) : undefined,
        });
    }

    close(): void {
        for (const peer of this.peers.values()) peer.close();
        this.peers.clear();
    }

    /** Build an RpcServiceDef from decorator metadata on a contract class. Override in FusionHub for compute support. */
    protected _buildServiceDef(
        cls: AnyConstructor
    ): RpcServiceDef {
        const svcMeta = getServiceMeta(cls);
        if (svcMeta === undefined)
            throw new Error('Contract class missing @rpcService metadata');

        const methodsMeta = getMethodsMeta(cls) ?? {};
        const methods = new Map<string, RpcMethodDef>();

        for (const [name, meta] of Object.entries(methodsMeta)) {
            const wireArgCount = meta.argCount + 1;
            const mapKey = `${name}:${wireArgCount}`;
            const isNoWait = meta.returns === RpcType.noWait;
            methods.set(mapKey, {
                name,
                serviceName: svcMeta.name,
                argCount: meta.argCount,
                wireArgCount,
                callTypeId: 0,
                stream: meta.returns === RpcType.stream,
                noWait: isNoWait,
                remoteExecutionMode: isNoWait
                    ? 0
                    : (meta.remoteExecutionMode ?? RpcRemoteExecutionMode.Default),
            });
        }

        return { name: svcMeta.name, methods };
    }

    /** Resolve a service def from either a plain RpcServiceDef or a contract class. */
    protected _resolveServiceDef(
        defOrContract: RpcServiceDef | (AnyConstructor)
    ): RpcServiceDef {
        if (typeof defOrContract === 'function')
            return this._buildServiceDef(defOrContract);
        return defOrContract;
    }

    /** Override in FusionHub — default is pass-through (for methods with callTypeId !== 0). */
    protected _wrapServerMethod(
        _methodDef: RpcMethodDef,
        fn: (...args: unknown[]) => unknown,
        _impl: object
    ): (...args: unknown[]) => unknown {
        return fn;
    }

    /** Create a client method for the given methodDef. Override in FusionHub for custom call types. */
    protected _createClientMethod(
        peer: RpcPeer,
        methodDef: RpcMethodDef
    ): (...args: unknown[]) => unknown {
        const wireName = wireMethodName(methodDef);
        const mode = methodDef.remoteExecutionMode;
        const mustCheckConnection = !(mode & RpcRemoteExecutionMode.AwaitForConnection);

        // NoWait methods — remoteExecutionMode is always 0, silently drop if disconnected
        if (methodDef.noWait) {
            return (...args: unknown[]) => {
                const callArgs = args.slice(0, methodDef.argCount);
                peer.callNoWait(wireName, callArgs);
            };
        }

        // Stream methods — call returns an RpcStream<T> (AsyncIterable)
        if (methodDef.stream) {
            return async (...args: unknown[]) => {
                if (mustCheckConnection && !peer.isConnected)
                    throw new Error('Outbound call failed: not connected and AwaitForConnection is not set.');
                const callArgs = args.slice(0, methodDef.argCount);
                const outboundCall = peer.call(wireName, callArgs);
                const result = await outboundCall.result.promise;
                const ref = parseStreamRef(result);
                if (!ref)
                    throw new Error(
                        `Expected stream reference, got: ${JSON.stringify(result)}`
                    );
                const stream = new RpcStream(ref, peer);
                peer.remoteObjects.register(stream);
                return stream;
            };
        }

        // Regular methods (including non-zero callTypeId — subclass can override for custom behavior)
        const callOptions: RpcCallOptions | undefined =
            (methodDef.callTypeId !== 0 || mode !== RpcRemoteExecutionMode.Default)
                ? { callTypeId: methodDef.callTypeId, remoteExecutionMode: mode }
                : undefined;

        return async (...args: unknown[]) => {
            if (mustCheckConnection && !peer.isConnected)
                throw new Error('Outbound call failed: not connected and AwaitForConnection is not set.');
            const callArgs = args.slice(0, methodDef.argCount);
            const outboundCall = peer.call(wireName, callArgs, callOptions);
            const result = await outboundCall.result.promise;
            return resolveStreamRefs(result, peer);
        };
    }
}
