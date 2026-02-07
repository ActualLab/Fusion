import { serializeMessage, RpcSystemCalls, type RpcServiceDef } from "@actuallab/rpc";
import type { RpcConnection } from "@actuallab/rpc";
import { ComputeFunction, type ComputeFn } from "./compute-function.js";

/** Server-side host for compute services — runs compute functions, sends $sys-c.Invalidate on invalidation. */
export class ComputeServiceHost {
  private _computeFunctions = new Map<string, ComputeFunction>();
  private _serviceDef: RpcServiceDef;

  constructor(serviceDef: RpcServiceDef, impl: Record<string, ComputeFn>) {
    this._serviceDef = serviceDef;
    for (const [methodName, fn] of Object.entries(impl)) {
      const methodDef = serviceDef.methods.get(methodName);
      if (methodDef?.compute) {
        this._computeFunctions.set(
          methodName,
          new ComputeFunction(serviceDef.name, methodName, fn),
        );
      }
    }
  }

  get serviceDef(): RpcServiceDef {
    return this._serviceDef;
  }

  async handleComputeCall(
    methodName: string,
    args: unknown[],
    callId: number,
    connection: RpcConnection,
  ): Promise<unknown> {
    const computeFn = this._computeFunctions.get(methodName);
    if (computeFn === undefined) {
      throw new Error(`Not a compute method: ${this._serviceDef.name}.${methodName}`);
    }

    const computed = await computeFn.invoke(args);

    // Watch for invalidation → send $sys-c.Invalidate to client
    computed.onInvalidated = () => {
      if (!connection.isOpen) return;
      const msg = serializeMessage(
        { Method: RpcSystemCalls.invalidate, RelatedId: callId },
      );
      connection.send(msg);
    };

    return computed.value;
  }

  isComputeMethod(methodName: string): boolean {
    return this._computeFunctions.has(methodName);
  }
}
