// .NET counterpart: ActualLab.Rpc.RpcCallTimeouts (+ RpcCallTimeouts.Default).
// TS omits the query/command classification .NET derives from method metadata
// (the port has no RpcMethodKind), so timeouts are opt-in per call or per method
// def; the default is unbounded (the .NET query default).

/**
 * Connect/run timeouts for an outbound RPC call, enforced by the peer's
 * maintenance loop. `Infinity` means "no timeout".
 */
export class RpcCallTimeouts {
    /** Both timeouts unbounded — the query default (.NET `RpcCallTimeouts.None`). */
    static readonly None = new RpcCallTimeouts(Infinity, Infinity);
    /** Command default: 1.5 s connect / 10 s run (.NET `RpcCallTimeouts.Default.Command`). */
    static readonly Command = new RpcCallTimeouts(1_500, 10_000);

    /** Max time a call may wait for the connection before it is sent. */
    readonly connectTimeoutMs: number;
    /** Max time a sent call may stay unanswered before it is failed. */
    readonly runTimeoutMs: number;

    constructor(connectTimeoutMs = Infinity, runTimeoutMs = Infinity) {
        this.connectTimeoutMs = connectTimeoutMs;
        this.runTimeoutMs = runTimeoutMs;
    }
}
