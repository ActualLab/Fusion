/**
 * Configurable timing limits for RPC connection lifecycle. Mirrors .NET's
 * `RpcLimits` (src/ActualLab.Rpc/Configuration/RpcLimits.cs).
 *
 * Override paths, from broadest to narrowest:
 *  - **Process-wide**: mutate fields on {@link RpcLimits.Default} (affects
 *    every hub that hasn't been given its own instance — hubs default to
 *    using the shared `Default`, so mutations propagate live), or replace
 *    `RpcLimits.Default = new RpcLimits({ … })` (affects hubs constructed
 *    *after* the replacement).
 *  - **Per-hub**: assign `hub.limits = new RpcLimits({ … })` before peers
 *    are created from that hub.
 *  - **Per-peer**: set the matching `*Ms` field on the peer directly
 *    (e.g. `peer.keepAliveTimeoutMs = 60_000`) before `start()`.
 *
 * Peers snapshot the relevant values from `hub.limits` at construction —
 * later mutations to `hub.limits` don't retroactively affect already-built
 * peers; that's by design and matches the .NET semantics.
 */
export class RpcLimits {
    /** Process-wide default. Hubs read from this when they're constructed
     *  unless caller assigns a custom instance to `hub.limits`. */
    static Default: RpcLimits = new RpcLimits();

    /** Max time to wait for the WebSocket to enter the OPEN state. On a
     *  hung connect (mobile after network change, half-open after sleep)
     *  the browser can take ~2 min to emit `onerror`/`onclose`; without
     *  this cap the reconnect loop blocks for that whole window. */
    connectTimeoutMs = 10_000;

    /** Max time to wait for the server's handshake response after WS opens. */
    handshakeTimeoutMs = 10_000;

    /** Outbound `$sys.KeepAlive` send period. */
    keepAlivePeriodMs = 10_000;

    /** Reaper threshold — connection is force-closed (and the reconnect
     *  loop takes over) if no inbound `$sys.KeepAlive` has been seen for
     *  this long. Sized to tolerate a complete server stall plus most of
     *  one keepalive cycle: the worst-case age of `_lastKeepAliveAt` is
     *  `keepAlivePeriodMs + stall_duration`, so with these defaults the
     *  tolerated stall is ~15 s. */
    keepAliveTimeoutMs = 25_000;

    constructor(overrides?: Partial<RpcLimits>) {
        if (overrides) Object.assign(this, overrides);
    }
}
