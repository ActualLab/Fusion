import type { IRpcObject } from './rpc-object.js';

export class RpcRemoteObjectTracker {
    private _objects = new Map<number, IRpcObject>();

    register(obj: IRpcObject): void {
        const localId = obj.id.localId;
        const existing = this._objects.get(localId);
        if (existing === obj)
            return;

        // A different live object shares this localId — happens after switching
        // to another peer instance (e.g. via LB). Disconnect the stale one first.
        if (existing !== undefined)
            existing.disconnect();

        this._objects.set(localId, obj);
    }

    get(localId: number): IRpcObject | undefined {
        return this._objects.get(localId);
    }

    keys(): IterableIterator<number> {
        return this._objects.keys();
    }

    unregister(obj: IRpcObject): void {
        const localId = obj.id.localId;
        if (this._objects.get(localId) === obj)
            this._objects.delete(localId);
    }

    disconnectAll(): void {
        for (const obj of this._objects.values()) {
            obj.disconnect();
        }
        this._objects.clear();
    }

    reconnectAll(): void {
        for (const obj of this._objects.values()) {
            if (obj.allowReconnect) obj.reconnect();
            else obj.disconnect();
        }
    }
}
