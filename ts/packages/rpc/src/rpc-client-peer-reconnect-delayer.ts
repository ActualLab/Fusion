// .NET counterpart: ActualLab.Rpc.RpcClientPeerReconnectDelayer

import { RetryDelaySeq, RetryDelayer } from '@actuallab/core';

export class RpcClientPeerReconnectDelayer extends RetryDelayer {
    constructor() {
        super();
        // Fixed 0.1s — callers gate *whether* to reconnect via app-level
        // signals (see Api / ApiReconnectDelayer). Exponential backoff would
        // just add latency on a flaky drop once the gate says "go".
        this.delays = RetryDelaySeq.fixed(100);
    }
}
