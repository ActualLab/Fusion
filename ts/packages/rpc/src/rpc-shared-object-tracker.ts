import type { IRpcObject } from "./rpc-object.js";

export class RpcSharedObjectTracker {
  private _nextId = 1;
  private _objects = new Map<number, IRpcObject>();

  nextId(): number {
    return this._nextId++;
  }

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
}
