// .NET counterparts:
//   RpcServiceDef (Configuration/) — 162 lines.  Describes a registered service
//     including its .NET Type, Mode (Local/Client/Server/Hybrid), ServerResolver
//     (DI-based lazy resolution), ClientType, IsSystem, IsBackend flags, Scope,
//     LegacyNames, PropertyBag, and a method dictionary built via reflection.
//   RpcMethodDef (Configuration/) — 189 lines.  Describes a single method:
//     FullName (ServiceName.MethodName:ArgCount), CallType (Regular/Reliable),
//     NoWait, HasPolymorphicArguments/Result, RpcMethodKind (Query/Command/System),
//     RpcMethodAttribute, LegacyNames, Tracer, outbound/inbound call pipeline
//     (factories, invokers, middleware chains, routers, timeouts).
//
// Omitted from .NET:
//   - Reflection-based method discovery (BuildMethods) — .NET scans interface
//     methods via reflection, filters generics, builds call pipelines.  TS uses
//     explicit defineRpcService() or decorator metadata — no runtime reflection.
//   - RpcMethodKind (Query/Command/System/Other) — .NET classifies methods to
//     route Commands through ICommander.  TS has no command bus; the distinction
//     is unnecessary.
//   - IsBackend / IBackendService — .NET separates "backend-only" services that
//     require backend auth.  TS client never exposes backend services.
//   - RpcMethodAttribute / LegacyNames — .NET supports custom wire names and
//     version-specific legacy name mappings for rolling upgrades.  TS has a
//     single version; wire names = "ServiceName.MethodName:wireArgCount".
//   - Call pipeline (OutboundCallFactory, InboundCallFactory, InboundCallInvoker,
//     MiddlewareFilter, OutboundCallRouter, OutboundCallTimeouts) — .NET builds a
//     per-method middleware chain at startup via generic factory types.  TS dispatches
//     directly from RpcServiceHost; no middleware or routing layer.
//   - CallType (Regular/Reliable) — Reliable calls track stages for reconnect
//     resumption.  TS replays entire calls on reconnect.
//   - HasPolymorphicArguments/Result — governs serializer behavior for abstract
//     argument types.  TS uses JSON (inherently polymorphic); not applicable.
//   - Tracer / RpcCallTracer — per-method distributed tracing.  Not ported.
//   - PropertyBag — extensible metadata on service/method def.  Not needed.
//   - Mode (ServiceMode) / Scope / ServerResolver / ClientType — DI wiring.

/** Describes a single method on an RPC service. */
export interface RpcMethodDef {
  readonly name: string;
  readonly serviceName: string;
  readonly argCount: number;
  readonly wireArgCount: number;
  readonly callTypeId: number;
  readonly stream: boolean;
  readonly noWait: boolean;
}

/** Describes an RPC service — its name and method definitions. */
export interface RpcServiceDef {
  readonly name: string;
  readonly methods: ReadonlyMap<string, RpcMethodDef>;
}

export interface RpcMethodDefInput {
  args: unknown[];
  returns?: symbol;
  callTypeId?: number;
  /** Override for wireArgCount. Default: args.length + 1 (assumes a CancellationToken slot). */
  wireArgCount?: number;
}

export const RpcType = {
  object: Symbol("object"),
  stream: Symbol("stream"),
  noWait: Symbol("noWait"),
  void: Symbol("void"),
} as const;

/** Returns the full wire method name: "ServiceName.MethodName:wireArgCount". */
export function wireMethodName(def: RpcMethodDef): string {
  return `${def.serviceName}.${def.name}:${def.wireArgCount}`;
}

export function defineRpcService(
  name: string,
  methods: Record<string, RpcMethodDefInput>,
): RpcServiceDef {
  const map = new Map<string, RpcMethodDef>();
  for (const [key, input] of Object.entries(methods)) {
    // If the key contains ":", the part before ":" is the clean name
    // (the suffix is for JS object key disambiguation of overloads).
    // Otherwise the key IS the clean name.
    const colonIndex = key.indexOf(":");
    const cleanName = colonIndex >= 0 ? key.substring(0, colonIndex) : key;
    const wireArgCount = input.wireArgCount ?? (input.args.length + 1);
    const mapKey = `${cleanName}:${wireArgCount}`;
    map.set(mapKey, {
      name: cleanName,
      serviceName: name,
      argCount: input.args.length,
      wireArgCount,
      callTypeId: input.callTypeId ?? 0,
      stream: input.returns === RpcType.stream,
      noWait: input.returns === RpcType.noWait,
    });
  }
  return { name, methods: map };
}
