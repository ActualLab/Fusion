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

  disconnectAll(): void {
    for (const obj of this._objects.values()) {
      obj.disconnect();
    }
    this._objects.clear();
  }

  reconnectAll(): void {
    for (const obj of this._objects.values()) {
      obj.reconnect();
    }
  }
}
