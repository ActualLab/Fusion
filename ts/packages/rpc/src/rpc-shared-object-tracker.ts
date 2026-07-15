import type { IRpcObject } from './rpc-object.js';

export class RpcSharedObjectTracker {
    private _nextId = 1;
    private _objects = new Map<number, IRpcObject>();

    nextId(): number {
        return this._nextId++;
    }

    register(obj: IRpcObject): void {
        const localId = obj.id.localId;
        if (this._objects.has(localId))
            throw new Error(`RPC shared object with localId ${localId} is already registered.`);

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
}
