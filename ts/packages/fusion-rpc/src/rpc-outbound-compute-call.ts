import { PromiseSource } from '@actuallab/core';
import { RpcOutboundCall } from '@actuallab/rpc';

/** Tracks a pending outbound compute RPC call — stays in tracker until invalidated. */
export class RpcOutboundComputeCall extends RpcOutboundCall {
    override readonly removeOnOk = false;
    readonly whenInvalidated = new PromiseSource<void>();

    // A completed compute call is always self-invalidated on reconnect (the
    // documented invalidate-on-reconnect simplification, F6), so it must not be
    // reported to the server in `$sys.Reconnect` — that would re-arm stage-2
    // invalidation tracking for a computed we're about to drop. Report null,
    // mirroring C# RpcOutboundComputeCall.GetReconnectStage's peer-change branch.
    override getReconnectStage(isPeerChanged: boolean): number | null {
        if (this.result.isCompleted)
            return null;

        return super.getReconnectStage(isPeerChanged);
    }

    override onDisconnect(): void {
        this.whenInvalidated.resolve();
    }
}
