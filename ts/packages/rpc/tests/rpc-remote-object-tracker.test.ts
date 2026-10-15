import { describe, it, expect } from 'vitest';
import { RpcRemoteObjectTracker } from '../src/rpc-remote-object-tracker.js';
import type { IRpcObject } from '../src/rpc-object.js';

class StubObject {
    reconnectCount = 0;
    disconnectCount = 0;
    readonly id: { localId: number };
    readonly kind = 0;

    constructor(
        localId: number,
        readonly allowReconnect: boolean,
        private readonly _failReconnect = false
    ) {
        this.id = { localId };
    }

    reconnect(): void {
        this.reconnectCount++;
        if (this._failReconnect)
            throw new Error('Simulated reconnect failure');
    }

    disconnect(): void {
        this.disconnectCount++;
    }
}

describe('RpcRemoteObjectTracker.reconnectAll', () => {
    it('reconnects objects that allow it and disconnects the rest', () => {
        const tracker = new RpcRemoteObjectTracker();
        const reconnectable = new StubObject(1, true);
        const fixed = new StubObject(2, false);
        register(tracker, reconnectable, fixed);

        tracker.reconnectAll();

        expect(reconnectable.reconnectCount).toBe(1);
        expect(reconnectable.disconnectCount).toBe(0);
        expect(fixed.reconnectCount).toBe(0);
        expect(fixed.disconnectCount).toBe(1);
    });

    it('disconnects an object that fails to reconnect and still reconnects the others', () => {
        const tracker = new RpcRemoteObjectTracker();
        const first = new StubObject(1, true);
        const failing = new StubObject(2, true, true);
        const last = new StubObject(3, true);
        register(tracker, first, failing, last);

        expect(() => tracker.reconnectAll()).not.toThrow();

        expect(first.reconnectCount).toBe(1);
        expect(failing.reconnectCount).toBe(1);
        expect(failing.disconnectCount).toBe(1);
        expect(last.reconnectCount).toBe(1);
        expect(last.disconnectCount).toBe(0);
    });

    function register(tracker: RpcRemoteObjectTracker, ...objects: StubObject[]): void {
        for (const obj of objects)
            tracker.register(obj as unknown as IRpcObject);
    }
});
