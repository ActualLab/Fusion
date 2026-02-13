import { PromiseSource } from "@actuallab/core";
import { RpcOutboundCall } from "@actuallab/rpc";

/** Tracks a pending outbound compute RPC call — stays in tracker until invalidated. */
export class RpcOutboundComputeCall extends RpcOutboundCall {
  override readonly removeOnOk = false;
  readonly whenInvalidated = new PromiseSource<void>();

  override onDisconnect(): void {
    this.whenInvalidated.resolve();
  }
}
