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

import { RpcClientPeer, RpcServerPeer, type RpcPeer, type RpcCallOptions } from "./rpc-peer.js";
import { RpcServiceHost, type RpcServiceImpl } from "./rpc-service-host.js";
import type { RpcServiceDef, RpcMethodDef } from "./rpc-service-def.js";
import { wireMethodName, RpcType } from "./rpc-service-def.js";
import { getServiceMeta, getMethodsMeta } from "./rpc-decorators.js";
import { RpcSystemCallSender } from "./rpc-system-call-sender.js";
import { RpcSystemCallHandler } from "./rpc-system-call-handler.js";
import { RpcStream, parseStreamRef, resolveStreamRefs } from "./rpc-stream.js";

/** Central RPC coordinator — manages peers, services, and configuration. */
export class RpcHub {
  readonly hubId: string;
  readonly peers = new Map<string, RpcPeer>();
  readonly serviceHost: RpcServiceHost;
  readonly systemCallSender = new RpcSystemCallSender();
  systemCallHandler: RpcSystemCallHandler = new RpcSystemCallHandler();

  constructor(hubId?: string) {
    this.hubId = hubId ?? crypto.randomUUID();
    this.serviceHost = new RpcServiceHost();
  }

  addPeer(peer: RpcPeer): void {
    this.peers.set(peer.ref, peer);
  }

  removePeer(ref: string): void {
    const peer = this.peers.get(ref);
    if (peer !== undefined) {
      peer.close();
      this.peers.delete(ref);
    }
  }

  /** Get or create a peer by ref. Server peers have "server://" prefix, everything else is a client peer. */
  getPeer(ref: string): RpcPeer {
    let peer = this.peers.get(ref);
    if (peer) return peer;
    peer = ref.startsWith("server://")
      ? new RpcServerPeer(this, ref)
      : new RpcClientPeer(this, ref);
    this.addPeer(peer);
    return peer;
  }

  /** Get or create a client peer for the given URL. */
  getClientPeer(ref: string): RpcClientPeer {
    return this.getPeer(ref) as RpcClientPeer;
  }

  /** Get or create a server peer for the given ref. */
  getServerPeer(ref: string): RpcServerPeer {
    return this.getPeer(ref) as RpcServerPeer;
  }

  /** Register a service with optional server method wrapping for custom call types. */
  addService(defOrContract: RpcServiceDef | (abstract new (...args: any[]) => any), impl: RpcServiceImpl): void {
    const def = this._resolveServiceDef(defOrContract);
    const wrappedImpl: RpcServiceImpl = {};
    for (const methodDef of def.methods.values()) {
      const fn = impl[methodDef.name];
      if (!fn) continue;
      wrappedImpl[methodDef.name] = methodDef.callTypeId !== 0
        ? this._wrapServerMethod(methodDef, fn, impl as object)
        : fn;
    }
    this.serviceHost.register(def, wrappedImpl);
  }

  /** Create a typed client proxy for a service on a remote peer. */
  addClient<T extends object>(peer: RpcPeer, defOrContract: RpcServiceDef | (abstract new (...args: any[]) => any)): T {
    const def = this._resolveServiceDef(defOrContract);

    // Group methods by clean name, indexed by argCount for overload resolution
    const overloads = new Map<string, Map<number, Function>>();
    for (const methodDef of def.methods.values()) {
      let byArgCount = overloads.get(methodDef.name);
      if (!byArgCount) { byArgCount = new Map(); overloads.set(methodDef.name, byArgCount); }
      byArgCount.set(methodDef.argCount, this._createClientMethod(peer, methodDef));
    }

    // Build final proxy methods — single overload: use directly; multiple: resolve by args.length
    const methods = new Map<string, Function>();
    for (const [name, byArgCount] of overloads) {
      if (byArgCount.size === 1) {
        methods.set(name, byArgCount.values().next().value!);
      } else {
        methods.set(name, (...args: unknown[]) => {
          const fn = byArgCount.get(args.length);
          if (!fn) throw new Error(`No overload of ${name} accepts ${args.length} arguments`);
          return fn(...args);
        });
      }
    }

    return new Proxy({} as T, {
      get: (_target, prop) => typeof prop === "string" ? methods.get(prop) : undefined,
    });
  }

  close(): void {
    for (const peer of this.peers.values()) peer.close();
    this.peers.clear();
  }

  /** Build an RpcServiceDef from decorator metadata on a contract class. Override in FusionHub for compute support. */
  protected _buildServiceDef(cls: abstract new (...args: any[]) => any): RpcServiceDef {
    const svcMeta = getServiceMeta(cls);
    if (svcMeta === undefined) throw new Error("Contract class missing @rpcService metadata");

    const methodsMeta = getMethodsMeta(cls) ?? {};
    const methods = new Map<string, RpcMethodDef>();

    for (const [name, meta] of Object.entries(methodsMeta)) {
      const wireArgCount = meta.argCount + 1;
      const mapKey = `${name}:${wireArgCount}`;
      methods.set(mapKey, {
        name,
        serviceName: svcMeta.name,
        argCount: meta.argCount,
        wireArgCount,
        callTypeId: 0,
        stream: meta.returns === RpcType.stream,
        noWait: meta.returns === RpcType.noWait,
      });
    }

    return { name: svcMeta.name, methods };
  }

  /** Resolve a service def from either a plain RpcServiceDef or a contract class. */
  protected _resolveServiceDef(defOrContract: RpcServiceDef | (abstract new (...args: any[]) => any)): RpcServiceDef {
    if (typeof defOrContract === "function")
      return this._buildServiceDef(defOrContract);
    return defOrContract;
  }

  /** Override in FusionHub — default is pass-through (for methods with callTypeId !== 0). */
  protected _wrapServerMethod(
    _methodDef: RpcMethodDef,
    fn: (...args: unknown[]) => unknown, _impl: object,
  ): (...args: unknown[]) => unknown {
    return fn;
  }

  /** Create a client method for the given methodDef. Override in FusionHub for custom call types. */
  protected _createClientMethod(
    peer: RpcPeer, methodDef: RpcMethodDef,
  ): (...args: unknown[]) => unknown {
    const wireName = wireMethodName(methodDef);

    // NoWait methods
    if (methodDef.noWait) {
      return (...args: unknown[]) => {
        const callArgs = args.slice(0, methodDef.argCount);
        peer.callNoWait(wireName, callArgs);
      };
    }

    // Stream methods — call returns an RpcStream<T> (AsyncIterable)
    if (methodDef.stream) {
      return async (...args: unknown[]) => {
        const callArgs = args.slice(0, methodDef.argCount);
        const outboundCall = peer.call(wireName, callArgs);
        const result = await outboundCall.result.promise;
        const ref = parseStreamRef(result);
        if (!ref) throw new Error(`Expected stream reference, got: ${JSON.stringify(result)}`);
        const stream = new RpcStream(ref, peer);
        peer.remoteObjects.register(stream);
        return stream;
      };
    }

    // Regular methods (including non-zero callTypeId — subclass can override for custom behavior)
    const callOptions: RpcCallOptions | undefined = methodDef.callTypeId !== 0
      ? { callTypeId: methodDef.callTypeId }
      : undefined;

    return async (...args: unknown[]) => {
      const callArgs = args.slice(0, methodDef.argCount);
      const outboundCall = peer.call(wireName, callArgs, callOptions);
      const result = await outboundCall.result.promise;
      return resolveStreamRefs(result, peer);
    };
  }
}
