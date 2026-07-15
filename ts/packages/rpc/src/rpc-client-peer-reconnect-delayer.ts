// .NET counterpart: ActualLab.Rpc.RpcClientPeerReconnectDelayer

import { RetryDelaySeq, RetryDelayer } from '@actuallab/core';

export class RpcClientPeerReconnectDelayer extends RetryDelayer {
    constructor() {
        super();
        // 0.1s base — a single flaky drop still reconnects fast (callers gate
        // *whether* to reconnect via app-level signals, see Api /
        // ApiReconnectDelayer). The exponential tail matters when `_tryIndex`
        // keeps climbing — repeated connect failures and premature disconnects
        // (RpcLimits.prematureDisconnectTimeoutMs) — so clients back off to
        // 10s instead of hammering a crash-looping server ~10x/s. C# uses
        // Exp(1s, 60s) for browser clients (RpcClientPeerReconnectDelayer.cs).
        this.delays = RetryDelaySeq.exp(100, 10_000);
    }
}
