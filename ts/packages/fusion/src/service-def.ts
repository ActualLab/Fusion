/** Describes a single method on a service — argument types, return type, compute flag. */
export interface MethodDef {
  readonly name: string;
  readonly serviceName: string;
  readonly argCount: number;
  readonly compute: boolean;
  readonly stream: boolean;
}

/** Describes a service — its name and method definitions. */
export interface ServiceDef {
  readonly name: string;
  readonly methods: ReadonlyMap<string, MethodDef>;
  readonly compute: boolean;
}

export interface MethodDefInput {
  args: unknown[];
  returns?: symbol;
  compute?: boolean;
  stream?: boolean;
}

export const RpcType = {
  object: Symbol("object"),
  stream: Symbol("stream"),
  void: Symbol("void"),
} as const;

export function defineRpcService(
  name: string,
  methods: Record<string, MethodDefInput>,
): ServiceDef {
  return buildServiceDef(name, methods, false);
}

export function defineComputeService(
  name: string,
  methods: Record<string, MethodDefInput>,
): ServiceDef {
  return buildServiceDef(name, methods, true);
}

function buildServiceDef(
  name: string,
  methods: Record<string, MethodDefInput>,
  defaultCompute: boolean,
): ServiceDef {
  const map = new Map<string, MethodDef>();
  for (const [methodName, input] of Object.entries(methods)) {
    map.set(methodName, {
      name: methodName,
      serviceName: name,
      argCount: input.args.length,
      compute: input.compute ?? defaultCompute,
      stream: input.stream ?? (input.returns === RpcType.stream),
    });
  }
  return { name, methods: map, compute: defaultCompute };
}
