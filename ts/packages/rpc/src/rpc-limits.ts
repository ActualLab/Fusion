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

    /** Dev preset with a relaxed keep-alive timeout (5 min), matching .NET's
     *  debugger-attached `KeepAlivePeriod` (RpcLimits.cs:46-54). Activate
     *  explicitly — e.g. `RpcLimits.Default = RpcLimits.Debug` or per-hub
     *  `hub.limits = RpcLimits.Debug`. Never switched on automatically: the
     *  TS side can't detect a debugger on the server (decision D4). */
    static readonly Debug: RpcLimits = new RpcLimits({ keepAliveTimeoutMs: 300_000 });

    /** Max time to wait for the WebSocket to enter the OPEN state. On a
     *  hung connect (mobile after network change, half-open after sleep)
     *  the browser can take ~2 min to emit `onerror`/`onclose`; without
     *  this cap the reconnect loop blocks for that whole window. */
    connectTimeoutMs = 10_000;

    /** Max time to wait for the server's handshake response after WS opens. */
    handshakeTimeoutMs = 10_000;

    /** If a connection lived less than this before dropping, a graceful close
     *  still counts as a failed attempt — the client keeps its growing
     *  `_tryIndex` instead of resetting it, so a crash-looping server sees
     *  increasing reconnect delays via `RetryDelaySeq`. Mirrors .NET
     *  `RpcLimits.PrematureDisconnectTimeout` (RpcLimits.cs:18). */
    prematureDisconnectTimeoutMs = 15_000;

    /** Max completed inbound calls retained for duplicate-frame dedup
     *  (a resent call id re-sends the computed result instead of
     *  re-executing the handler). Deviation from .NET, which unregisters a
     *  call once its result is sent: TS clients blind-resend on reconnect,
     *  so completed calls are retained — bounded by this limit (oldest
     *  evicted first) to cap memory on long-lived connections. */
    completedInboundCallsLimit = 1000;

    /** How often each peer's maintenance loop scans outbound calls for
     *  timeouts (R12). Mirrors .NET `RpcLimits.CallTimeoutCheckPeriod`. */
    callTimeoutCheckPeriodMs = 1_000;

    /** Outbound `$sys.KeepAlive` send period. */
    keepAlivePeriodMs = 10_000;

    /** Reaper threshold — connection is force-closed (and the reconnect
     *  loop takes over) if no inbound `$sys.KeepAlive` has been seen for
     *  this long. Sized to tolerate a complete server stall plus most of
     *  one keepalive cycle: the worst-case age of `_lastKeepAliveAt` is
     *  `keepAlivePeriodMs + stall_duration`, so with these defaults the
     *  tolerated stall is ~15 s. */
    keepAliveTimeoutMs = 25_000;

    /** How long a server peer stays in `hub.peers` after its connection closes
     *  before it's stopped and removed. A same-peer reconnect (`accept()`)
     *  within this window cancels the pending removal, so brief drops don't
     *  discard the peer's trackers. Mirrors .NET's server-peer shutdown timeout
     *  (`RpcPeerOptions.ServerPeerShutdownTimeoutProvider`, 3-15 min); the TS
     *  port uses a fixed floor-matching default. */
    serverPeerCloseTimeoutMs = 180_000;

    constructor(overrides?: Partial<RpcLimits>) {
        if (overrides) Object.assign(this, overrides);
    }
}
