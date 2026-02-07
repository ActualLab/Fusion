import type { MethodDef } from "./service-def.js";

/** Represents a single intercepted method call flowing through the interceptor chain. */
export interface Invocation {
  readonly service: unknown;
  readonly methodDef: MethodDef;
  readonly args: unknown[];
}
