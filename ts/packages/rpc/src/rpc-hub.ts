import type { RpcPeer } from "./rpc-peer.js";
import { RpcServiceHost, type RpcServiceImpl } from "./rpc-service-host.js";
import type { RpcServiceDef } from "./rpc-service-def.js";

/** Central RPC coordinator — manages peers, services, and configuration. */
export class RpcHub {
  readonly hubId: string;
  readonly peers = new Map<string, RpcPeer>();
  readonly serviceHost: RpcServiceHost;

  constructor(hubId?: string) {
    this.hubId = hubId ?? crypto.randomUUID();
    this.serviceHost = new RpcServiceHost();
  }

  addPeer(peer: RpcPeer): void {
    this.peers.set(peer.id, peer);
  }

  removePeer(id: string): void {
    const peer = this.peers.get(id);
    if (peer !== undefined) {
      peer.close();
      this.peers.delete(id);
    }
  }

  getPeer(id: string): RpcPeer | undefined {
    return this.peers.get(id);
  }

  registerService(def: RpcServiceDef, impl: RpcServiceImpl): void {
    this.serviceHost.register(def, impl);
  }

  close(): void {
    for (const peer of this.peers.values()) peer.close();
    this.peers.clear();
  }
}
