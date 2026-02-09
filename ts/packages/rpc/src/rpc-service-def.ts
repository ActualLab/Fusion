/** Describes a single method on an RPC service. */
export interface RpcMethodDef {
  readonly name: string;
  readonly serviceName: string;
  readonly argCount: number;
  readonly compute: boolean;
  readonly stream: boolean;
  readonly noWait: boolean;
}

/** Describes an RPC service — its name and method definitions. */
export interface RpcServiceDef {
  readonly name: string;
  readonly methods: ReadonlyMap<string, RpcMethodDef>;
  readonly compute: boolean;
}

export interface RpcMethodDefInput {
  args: unknown[];
  returns?: symbol;
  compute?: boolean;
  stream?: boolean;
  noWait?: boolean;
}

export const RpcType = {
  object: Symbol("object"),
  stream: Symbol("stream"),
  void: Symbol("void"),
} as const;

export function defineRpcService(
  name: string,
  methods: Record<string, RpcMethodDefInput>,
): RpcServiceDef {
  return buildServiceDef(name, methods, false);
}

export function defineComputeService(
  name: string,
  methods: Record<string, RpcMethodDefInput>,
): RpcServiceDef {
  return buildServiceDef(name, methods, true);
}

function buildServiceDef(
  name: string,
  methods: Record<string, RpcMethodDefInput>,
  defaultCompute: boolean,
): RpcServiceDef {
  const map = new Map<string, RpcMethodDef>();
  for (const [methodName, input] of Object.entries(methods)) {
    map.set(methodName, {
      name: methodName,
      serviceName: name,
      argCount: input.args.length,
      compute: input.compute ?? defaultCompute,
      stream: input.stream ?? (input.returns === RpcType.stream),
      noWait: input.noWait ?? false,
    });
  }
  return { name, methods: map, compute: defaultCompute };
}
