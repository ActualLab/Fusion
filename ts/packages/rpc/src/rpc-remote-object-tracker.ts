import type { IRpcObject } from "./rpc-object.js";

export class RpcRemoteObjectTracker {
  private _objects = new Map<number, IRpcObject>();

  register(obj: IRpcObject): void {
    this._objects.set(obj.id.localId, obj);
  }

  get(localId: number): IRpcObject | undefined {
    return this._objects.get(localId);
  }

  unregister(obj: IRpcObject): void {
    this._objects.delete(obj.id.localId);
  }

  /** Returns localIds of all registered remote objects — for KeepAlive. */
  activeIds(): number[] {
    return [...this._objects.keys()];
  }

  get size(): number {
    return this._objects.size;
  }

  /** Process server KeepAlive: disconnect remote objects not in the server's list. */
  keepAlive(serverIds: number[]): number[] {
    const serverSet = new Set(serverIds);
    const unknownIds: number[] = [];
    for (const id of serverIds) {
      if (!this._objects.has(id)) unknownIds.push(id);
    }
    return unknownIds;
  }

  /** Disconnect remote objects whose IDs aren't tracked by the server. */
  disconnectNotIn(serverIds: Set<number>): void {
    for (const [id, obj] of this._objects) {
      if (!serverIds.has(id)) {
        obj.disconnect();
        this._objects.delete(id);
      }
    }
  }

  disconnectAll(): void {
    for (const obj of this._objects.values()) {
      obj.disconnect();
    }
    this._objects.clear();
  }

  reconnectAll(): void {
    for (const obj of this._objects.values()) {
      if (obj.allowReconnect)
        obj.reconnect();
      else
        obj.disconnect();
    }
  }
}
