import {
  RpcHub,
  RpcClientPeer,
  RpcServerPeer,
  createMessageChannelPair,
} from "../src/index.js";

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** Manages a paired client-server connection for testing — supports connect/disconnect/reconnect/host-switch. */
export class RpcTestConnection {
  clientHub: RpcHub;
  serverHub: RpcHub;
  clientPeer: RpcClientPeer;
  serverPeer: RpcServerPeer | undefined;

  constructor(clientHub: RpcHub, serverHub: RpcHub, clientPeer: RpcClientPeer) {
    this.clientHub = clientHub;
    this.serverHub = serverHub;
    this.clientPeer = clientPeer;
  }

  async connect(): Promise<void> {
    const [clientConn, serverConn] = createMessageChannelPair();
    this.clientPeer.connectWith(clientConn);

    const serverId = crypto.randomUUID();
    this.serverPeer = new RpcServerPeer(serverId, this.serverHub, serverConn);
    this.serverHub.addPeer(this.serverPeer);

    await delay(1);
  }

  async disconnect(): Promise<void> {
    this.clientPeer.connection?.close();
    if (this.serverPeer !== undefined) {
      this.serverPeer.close();
      this.serverHub.peers.delete(this.serverPeer.id);
      this.serverPeer = undefined;
    }
    await delay(1);
  }

  async reconnect(delayMs = 10): Promise<void> {
    await this.disconnect();
    await delay(delayMs);
    await this.connect();
  }

  async switchHost(newServerHub: RpcHub): Promise<void> {
    this.serverHub = newServerHub;
    await this.reconnect();
  }
}

/** Background loop that repeatedly disconnects/reconnects at random intervals. */
export async function connectionDisruptor(
  conn: RpcTestConnection,
  signal: AbortSignal,
  connectedMs = [50, 150],
  disconnectedMs = [10, 40],
): Promise<void> {
  function randomInRange(range: number[]): number {
    const [min, max] = range;
    return min! + Math.random() * (max! - min!);
  }

  while (!signal.aborted) {
    // Stay connected for a random duration
    await delay(randomInRange(connectedMs));
    if (signal.aborted) break;

    await conn.disconnect();

    // Stay disconnected for a random duration
    await delay(randomInRange(disconnectedMs));
    if (signal.aborted) break;

    await conn.connect();
  }
}
