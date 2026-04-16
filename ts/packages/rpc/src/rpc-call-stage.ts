/**
 * Well-known RPC call completion stage constants, used by the
 * `$sys.Reconnect` protocol to tell the peer which calls have already
 * reached which stage and therefore don't need to be re-processed.
 *
 * Direct port of .NET `ActualLab.Rpc.Infrastructure.RpcCallStage` at
 * src/ActualLab.Rpc/Infrastructure/RpcCallStage.cs.
 */
export const RpcCallStage = {
    /** The call's result has been received from the peer. */
    ResultReady: 1,
    /** The compute call's result has been invalidated (compute-call specific). */
    Invalidated: 3,
    /**
     * Bit flag added on top of ResultReady/Invalidated when the outbound
     * call has been removed from the tracker (unregistered).
     */
    Unregistered: 0x1_000,
} as const;

export type RpcCallStage = number;
