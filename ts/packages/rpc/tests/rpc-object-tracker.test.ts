import { describe, it, expect } from 'vitest';
import {
    RpcRemoteObjectTracker,
    RpcSharedObjectTracker,
    RpcObjectKind,
} from '../src/index.js';
import type { IRpcObject, RpcObjectId } from '../src/index.js';

class FakeObject implements IRpcObject {
    readonly id: RpcObjectId;
    readonly kind: RpcObjectKind;
    readonly allowReconnect = true;
    disconnectCount = 0;

    constructor(localId: number, kind: RpcObjectKind) {
        this.id = { hostId: 'h', localId };
        this.kind = kind;
    }

    reconnect(): void {
        // no-op
    }
    disconnect(): void {
        this.disconnectCount++;
    }
}

describe('RpcRemoteObjectTracker identity invariants (R21)', () => {
    it('registering the same object twice is a no-op (no disconnect)', () => {
        const tracker = new RpcRemoteObjectTracker();
        const a = new FakeObject(1, RpcObjectKind.Remote);
        tracker.register(a);
        tracker.register(a);
        expect(a.disconnectCount).toBe(0);
        expect(tracker.get(1)).toBe(a);
    });

    it('registering a second object with the same id disconnects the displaced live object', () => {
        const tracker = new RpcRemoteObjectTracker();
        const a = new FakeObject(1, RpcObjectKind.Remote);
        const b = new FakeObject(1, RpcObjectKind.Remote);
        tracker.register(a);
        tracker.register(b);
        expect(a.disconnectCount).toBe(1);
        expect(tracker.get(1)).toBe(b);
    });

    it('ABA: unregister(A) after B replaced it does not remove B', () => {
        const tracker = new RpcRemoteObjectTracker();
        const a = new FakeObject(1, RpcObjectKind.Remote);
        const b = new FakeObject(1, RpcObjectKind.Remote);
        tracker.register(a);
        tracker.register(b);
        tracker.unregister(a);
        expect(tracker.get(1)).toBe(b);
    });

    it('unregister(obj) removes only when the entry still points to obj', () => {
        const tracker = new RpcRemoteObjectTracker();
        const a = new FakeObject(1, RpcObjectKind.Remote);
        tracker.register(a);
        tracker.unregister(a);
        expect(tracker.get(1)).toBeUndefined();
    });
});

describe('RpcSharedObjectTracker identity invariants (R21)', () => {
    it('rejects duplicate ids', () => {
        const tracker = new RpcSharedObjectTracker();
        const a = new FakeObject(1, RpcObjectKind.Local);
        const b = new FakeObject(1, RpcObjectKind.Local);
        tracker.register(a);
        expect(() => tracker.register(b)).toThrow(/already registered/);
        expect(tracker.get(1)).toBe(a);
    });

    it('unregister removes only the matching object', () => {
        const tracker = new RpcSharedObjectTracker();
        const a = new FakeObject(1, RpcObjectKind.Local);
        const other = new FakeObject(1, RpcObjectKind.Local);
        tracker.register(a);
        tracker.unregister(other); // same id, different object — no-op
        expect(tracker.get(1)).toBe(a);
        tracker.unregister(a);
        expect(tracker.get(1)).toBeUndefined();
    });
});
