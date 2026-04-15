import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcHub,
    RpcClientPeer,
    RpcSystemCallHandler,
    createMessageChannelPair,
    RpcObjectKind,
} from '../src/index.js';
import type { IRpcObject, RpcObjectId, RpcMessage } from '../src/index.js';
import type { RpcServerPeer } from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

class MockRpcObject implements IRpcObject {
    readonly id: RpcObjectId;
    readonly kind: RpcObjectKind;
    readonly allowReconnect = true;
    disconnected = false;
    reconnected = false;

    constructor(localId: number, kind: RpcObjectKind) {
        this.id = { hostId: 'mock', localId };
        this.kind = kind;
    }

    disconnect(): void {
        this.disconnected = true;
    }

    reconnect(): void {
        this.reconnected = true;
    }
}

describe('$sys.Disconnect handling', () => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let clientPeer: RpcClientPeer;
    let serverPeer: RpcServerPeer;
    let handler: RpcSystemCallHandler;

    beforeEach(async () => {
        serverHub = new RpcHub('server-hub');
        clientHub = new RpcHub('client-hub');

        const [cc, sc] = createMessageChannelPair();

        clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(cc);
        clientHub.addPeer(clientPeer);

        serverPeer = serverHub.getServerPeer('server://test');
        serverPeer.accept(sc);

        handler = new RpcSystemCallHandler();

        await delay(10);
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    it('should disconnect matching remote objects', () => {
        const obj1 = new MockRpcObject(101, RpcObjectKind.Remote);
        const obj2 = new MockRpcObject(102, RpcObjectKind.Remote);
        clientPeer.remoteObjects.register(obj1);
        clientPeer.remoteObjects.register(obj2);

        const message: RpcMessage = { Method: '$sys.Disconnect' };
        handler.handle(message, [[101, 102]], clientPeer);

        expect(obj1.disconnected).toBe(true);
        expect(obj2.disconnected).toBe(true);
    });

    it('should disconnect matching shared objects', () => {
        const obj = new MockRpcObject(201, RpcObjectKind.Local);
        clientPeer.sharedObjects.register(obj);

        const message: RpcMessage = { Method: '$sys.Disconnect' };
        handler.handle(message, [[201]], clientPeer);

        expect(obj.disconnected).toBe(true);
    });

    it('should disconnect both remote and shared objects with same id', () => {
        const remoteObj = new MockRpcObject(301, RpcObjectKind.Remote);
        const sharedObj = new MockRpcObject(301, RpcObjectKind.Local);
        clientPeer.remoteObjects.register(remoteObj);
        clientPeer.sharedObjects.register(sharedObj);

        const message: RpcMessage = { Method: '$sys.Disconnect' };
        handler.handle(message, [[301]], clientPeer);

        expect(remoteObj.disconnected).toBe(true);
        expect(sharedObj.disconnected).toBe(true);
    });

    it('should not error when object ids do not match any registered object', () => {
        const message: RpcMessage = { Method: '$sys.Disconnect' };
        // Should not throw
        handler.handle(message, [[999, 888]], clientPeer);
    });

    it('should only disconnect listed ids, not others', () => {
        const obj1 = new MockRpcObject(401, RpcObjectKind.Remote);
        const obj2 = new MockRpcObject(402, RpcObjectKind.Remote);
        clientPeer.remoteObjects.register(obj1);
        clientPeer.remoteObjects.register(obj2);

        const message: RpcMessage = { Method: '$sys.Disconnect' };
        handler.handle(message, [[401]], clientPeer);

        expect(obj1.disconnected).toBe(true);
        expect(obj2.disconnected).toBe(false);
    });

    it('should handle empty ids array', () => {
        const obj = new MockRpcObject(501, RpcObjectKind.Remote);
        clientPeer.remoteObjects.register(obj);

        const message: RpcMessage = { Method: '$sys.Disconnect' };
        handler.handle(message, [[]], clientPeer);

        expect(obj.disconnected).toBe(false);
    });

    it('should handle $sys.Disconnect with suffix', () => {
        const obj = new MockRpcObject(601, RpcObjectKind.Remote);
        clientPeer.remoteObjects.register(obj);

        const message: RpcMessage = { Method: '$sys.Disconnect:1' };
        handler.handle(message, [[601]], clientPeer);

        expect(obj.disconnected).toBe(true);
    });
});
