// .NET counterpart: ActualLab.Rpc.RpcClientPeerReconnectDelayer

import { RetryDelaySeq, RetryDelayer } from "@actuallab/core";

export class RpcClientPeerReconnectDelayer extends RetryDelayer {
  constructor() {
    super();
    this.delays = RetryDelaySeq.exp(1000, 60_000); // 1s min, 60s max
  }
}
