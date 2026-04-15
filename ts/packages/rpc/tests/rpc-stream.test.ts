import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcHub,
    RpcClientPeer,
    RpcStream,
    RpcStreamSender,
    RpcType,
    parseStreamRef,
    defineRpcService,
    createMessageChannelPair,
} from '../src/index.js';
import type { RpcServerPeer } from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('parseStreamRef', () => {
    it('should parse a valid stream reference string', () => {
        const ref = parseStreamRef('abc-123,42,10,3');
        expect(ref).not.toBeNull();
        expect(ref!.hostId).toBe('abc-123');
        expect(ref!.localId).toBe(42);
        expect(ref!.ackPeriod).toBe(10);
        expect(ref!.ackAdvance).toBe(3);
        expect(ref!.allowReconnect).toBe(true);
    });

    it('should parse allowReconnect from 5th field', () => {
        const ref1 = parseStreamRef('abc,1,10,3,1');
        expect(ref1).not.toBeNull();
        expect(ref1!.allowReconnect).toBe(true);

        const ref0 = parseStreamRef('abc,1,10,3,0');
        expect(ref0).not.toBeNull();
        expect(ref0!.allowReconnect).toBe(false);
    });

    it('should default allowReconnect to true when 5th field is missing', () => {
        const ref = parseStreamRef('abc,1,10,3');
        expect(ref).not.toBeNull();
        expect(ref!.allowReconnect).toBe(true);
    });

    it('should return null for non-string values', () => {
        expect(parseStreamRef(42)).toBeNull();
        expect(parseStreamRef(null)).toBeNull();
        expect(parseStreamRef(undefined)).toBeNull();
        expect(parseStreamRef({})).toBeNull();
    });

    describe('binary (MessagePack) format', () => {
        it('should parse binary format stream ref', () => {
            const ref = parseStreamRef({
                SerializedId: ['host-abc', 42],
                AckPeriod: 100,
                AckAdvance: 50,
                AllowReconnect: true,
            });
            expect(ref).not.toBeNull();
            expect(ref!.hostId).toBe('host-abc');
            expect(ref!.localId).toBe(42);
            expect(ref!.ackPeriod).toBe(100);
            expect(ref!.ackAdvance).toBe(50);
            expect(ref!.allowReconnect).toBe(true);
        });

        it('should use defaults when AckPeriod/AckAdvance are missing', () => {
            const ref = parseStreamRef({
                SerializedId: ['host-def', 7],
            });
            expect(ref).not.toBeNull();
            expect(ref!.hostId).toBe('host-def');
            expect(ref!.localId).toBe(7);
            expect(ref!.ackPeriod).toBe(256);
            expect(ref!.ackAdvance).toBe(128);
            expect(ref!.allowReconnect).toBe(true);
        });

        it('should parse AllowReconnect=false', () => {
            const ref = parseStreamRef({
                SerializedId: ['host-xyz', 99],
                AckPeriod: 64,
                AckAdvance: 32,
                AllowReconnect: false,
            });
            expect(ref).not.toBeNull();
            expect(ref!.allowReconnect).toBe(false);
        });

        it('should return null for invalid SerializedId (too short)', () => {
            expect(parseStreamRef({ SerializedId: ['only-one'] })).toBeNull();
        });

        it('should return null for missing SerializedId', () => {
            expect(parseStreamRef({ AckPeriod: 10 })).toBeNull();
        });

        it('should return null for non-array SerializedId', () => {
            expect(parseStreamRef({ SerializedId: 'not-an-array' })).toBeNull();
        });

        it('should return null for objects that are not stream refs', () => {
            expect(parseStreamRef({ foo: 'bar' })).toBeNull();
            expect(parseStreamRef({ name: 'test', value: 123 })).toBeNull();
        });
    });

    it('should return null for invalid format', () => {
        expect(parseStreamRef('a,b,c')).toBeNull(); // too few parts
        expect(parseStreamRef('a,b,c,d,e,f')).toBeNull(); // too many parts
        expect(parseStreamRef('a,b,c,d')).toBeNull(); // non-numeric localId
        expect(parseStreamRef('a,1,c,d')).toBeNull(); // non-numeric ackPeriod
    });
});

describe('RpcStream end-to-end', () => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let clientPeer: RpcClientPeer;
    let serverPeer: RpcServerPeer;

    const StreamServiceDef = defineRpcService('StreamService', {
        getNumbers: { args: [0], returns: RpcType.stream },
        getStrings: { args: [0], returns: RpcType.stream },
        failingStream: { args: [], returns: RpcType.stream },
        infiniteStream: { args: [], returns: RpcType.stream },
        emptyStream: { args: [], returns: RpcType.stream },
    });

    beforeEach(async () => {
        serverHub = new RpcHub('server-hub');
        clientHub = new RpcHub('client-hub');

        const [cc, sc] = createMessageChannelPair();

        clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(cc);
        clientHub.addPeer(clientPeer);

        serverPeer = serverHub.getServerPeer('server://test');
        serverPeer.accept(sc);

        await delay(10);
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    it('should stream items from server to client via async generator', async () => {
        serverHub.addService(StreamServiceDef, {
            getNumbers: async function* (count: unknown) {
                for (let i = 0; i < (count as number); i++) {
                    yield i * 10;
                }
            },
        });

        const client = clientHub.addClient<{
            getNumbers(count: number): Promise<AsyncIterable<number>>;
                }>(clientPeer, StreamServiceDef);

        const stream = await client.getNumbers(4);
        const items: number[] = [];
        for await (const item of stream) {
            items.push(item);
        }

        expect(items).toEqual([0, 10, 20, 30]);
    });

    it('should stream string items', async () => {
        serverHub.addService(StreamServiceDef, {
            getStrings: async function* (count: unknown) {
                const words = ['hello', 'world', 'foo', 'bar'];
                for (let i = 0; i < (count as number); i++) {
                    yield words[i % words.length]!;
                }
            },
        });

        const client = clientHub.addClient<{
            getStrings(count: number): Promise<AsyncIterable<string>>;
                }>(clientPeer, StreamServiceDef);

        const stream = await client.getStrings(3);
        const items: string[] = [];
        for await (const item of stream) {
            items.push(item);
        }

        expect(items).toEqual(['hello', 'world', 'foo']);
    });

    it('should handle empty stream', async () => {
        serverHub.addService(StreamServiceDef, {
            emptyStream: async function* () {
                // yield nothing
            },
        });

        const client = clientHub.addClient<{
            emptyStream(): Promise<AsyncIterable<number>>;
                }>(clientPeer, StreamServiceDef);

        const stream = await client.emptyStream();
        const items: number[] = [];
        for await (const item of stream) {
            items.push(item);
        }

        expect(items).toEqual([]);
    });

    it('should propagate server-side stream errors', async () => {
        serverHub.addService(StreamServiceDef, {
            failingStream: async function* () {
                yield 1;
                yield 2;
                throw new Error('Server stream failure');
            },
        });

        const client = clientHub.addClient<{
            failingStream(): Promise<AsyncIterable<number>>;
                }>(clientPeer, StreamServiceDef);

        const stream = await client.failingStream();
        const items: number[] = [];
        await expect(async () => {
            for await (const item of stream) {
                items.push(item);
            }
        }).rejects.toThrow('Server stream failure');

        expect(items).toEqual([1, 2]);
    });

    it('should support early break from client', async () => {
        let _yieldCount = 0;
        serverHub.addService(StreamServiceDef, {
            infiniteStream: async function* () {
                for (let i = 0; ; i++) {
                    _yieldCount++;
                    yield i;
                    // Small delay to give the client a chance to break
                    await delay(1);
                }
            },
        });

        const client = clientHub.addClient<{
            infiniteStream(): Promise<AsyncIterable<number>>;
                }>(clientPeer, StreamServiceDef);

        const stream = await client.infiniteStream();
        const items: number[] = [];
        for await (const item of stream) {
            items.push(item);
            if (items.length === 3) break;
        }

        expect(items).toEqual([0, 1, 2]);
    });

    it('should handle disconnect during streaming', async () => {
        serverHub.addService(StreamServiceDef, {
            infiniteStream: async function* () {
                for (let i = 0; ; i++) {
                    yield i;
                    await delay(5);
                }
            },
        });

        const client = clientHub.addClient<{
            infiniteStream(): Promise<AsyncIterable<number>>;
                }>(clientPeer, StreamServiceDef);

        const stream = await client.infiniteStream();
        const items: number[] = [];

        // Disconnect after collecting some items
        setTimeout(() => {
            clientPeer.close();
        }, 30);

        await expect(async () => {
            for await (const item of stream) {
                items.push(item);
            }
        }).rejects.toThrow('Peer disconnected.');

        expect(items.length).toBeGreaterThan(0);
    });

    it('should throw if client iterates the same stream twice', async () => {
        serverHub.addService(StreamServiceDef, {
            getNumbers: async function* (count: unknown) {
                for (let i = 0; i < (count as number); i++) yield i;
            },
        });

        const client = clientHub.addClient<{
            getNumbers(count: number): Promise<AsyncIterable<number>>;
                }>(clientPeer, StreamServiceDef);

        const stream = await client.getNumbers(1);
        const iter = stream[Symbol.asyncIterator]();

        expect(() => stream[Symbol.asyncIterator]()).toThrow(
            'RpcStream can only be iterated once'
        );

        await iter.return!();
    });

    it('should error on item gap when allowReconnect is false', async () => {
        const ref = { hostId: 'h', localId: 1, ackPeriod: 10, ackAdvance: 5, allowReconnect: false };
        const stream = new RpcStream<string>(ref, clientPeer);
        clientPeer.remoteObjects.register(stream);

        // Send item 0, then skip to item 5 (gap)
        stream.onItem(0, 'a');
        stream.onItem(5, 'f');

        const iter = stream[Symbol.asyncIterator]();
        const first = await iter.next();
        expect(first.value).toBe('a');

        await expect(iter.next()).rejects.toThrow(/gap/i);
    });

    it('should error on batch gap when allowReconnect is false', async () => {
        const ref = { hostId: 'h', localId: 2, ackPeriod: 10, ackAdvance: 5, allowReconnect: false };
        const stream = new RpcStream<string>(ref, clientPeer);
        clientPeer.remoteObjects.register(stream);

        // Send batch at index 0, then batch at index 10 (gap)
        stream.onBatch(0, ['a', 'b']);
        stream.onBatch(10, ['x', 'y']);

        const iter = stream[Symbol.asyncIterator]();
        const first = await iter.next();
        expect(first.value).toBe('a');
        const second = await iter.next();
        expect(second.value).toBe('b');

        await expect(iter.next()).rejects.toThrow(/gap/i);
    });

    it('should handle multiple concurrent streams', async () => {
        serverHub.addService(StreamServiceDef, {
            getNumbers: async function* (count: unknown) {
                for (let i = 0; i < (count as number); i++) {
                    yield i;
                }
            },
        });

        const client = clientHub.addClient<{
            getNumbers(count: number): Promise<AsyncIterable<number>>;
                }>(clientPeer, StreamServiceDef);

        const [stream1, stream2] = await Promise.all([
            client.getNumbers(3),
            client.getNumbers(2),
        ]);

        const [items1, items2] = await Promise.all([
            (async () => {
                const r: number[] = [];
                for await (const i of stream1) r.push(i);
                return r;
            })(),
            (async () => {
                const r: number[] = [];
                for await (const i of stream2) r.push(i);
                return r;
            })(),
        ]);

        expect(items1).toEqual([0, 1, 2]);
        expect(items2).toEqual([0, 1]);
    });
});

describe('RpcStream allowReconnect', () => {
    it('should disconnect instead of reconnect when allowReconnect is false', () => {
        const hub = new RpcHub('test-hub');
        const [cc] = createMessageChannelPair();
        const peer = new RpcClientPeer(hub, 'ws://test');
        peer.connectWith(cc);
        hub.addPeer(peer);

        const ref = {
            hostId: 'h',
            localId: 1,
            ackPeriod: 30,
            ackAdvance: 61,
            allowReconnect: false,
        };
        const stream = new RpcStream<number>(ref, peer);
        peer.remoteObjects.register(stream);

        // Start iterating so the stream is active
        const _iter = stream[Symbol.asyncIterator]();

        // Simulate reconnect — should disconnect instead
        stream.reconnect();

        // Stream should be completed with error
        expect((stream as any)._completed).toBe(true);
        expect((stream as any)._completionError).toBeInstanceOf(Error);
        expect((stream as any)._completionError.message).toBe(
            'Peer disconnected.'
        );

        hub.close();
    });

    it('should reconnect normally when allowReconnect is true', () => {
        const hub = new RpcHub('test-hub');
        const [cc] = createMessageChannelPair();
        const peer = new RpcClientPeer(hub, 'ws://test');
        peer.connectWith(cc);
        hub.addPeer(peer);

        const ref = {
            hostId: 'h',
            localId: 2,
            ackPeriod: 30,
            ackAdvance: 61,
            allowReconnect: true,
        };
        const stream = new RpcStream<number>(ref, peer);
        peer.remoteObjects.register(stream);

        stream.reconnect();

        // Stream should NOT be completed — it sent a reset ack instead
        expect((stream as any)._completed).toBe(false);
        expect((stream as any)._completionError).toBeNull();

        hub.close();
    });

    it('should include allowReconnect in RpcStreamSender.toRef()', () => {
        const hub = new RpcHub('test-hub');
        const [, sc] = createMessageChannelPair();
        const peer = hub.getServerPeer('server://test');
        peer.accept(sc);

        const sender1 = new RpcStreamSender<number>(peer, 30, 61, true);
        expect(sender1.toRef()).toContain(',1');

        const sender0 = new RpcStreamSender<number>(peer, 30, 61, false);
        expect(sender0.toRef().endsWith(',0')).toBe(true);

        hub.close();
    });

    it('should fail immediately on disconnect (end-to-end)', async () => {
        const serverHub = new RpcHub('server-hub');
        const clientHub = new RpcHub('client-hub');

        const [cc, sc] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(cc);
        clientHub.addPeer(clientPeer);

        const serverPeer = serverHub.getServerPeer('server://test');
        serverPeer.accept(sc);
        await delay(10);

        // Create sender with allowReconnect=false on server, stream on client
        const sender = new RpcStreamSender<number>(serverPeer, 30, 61, false);
        serverPeer.sharedObjects.register(sender);
        const ref = parseStreamRef(sender.toRef())!;
        expect(ref.allowReconnect).toBe(false);

        const stream = new RpcStream<number>(ref, clientPeer);
        clientPeer.remoteObjects.register(stream);

        // Start pumping items slowly
        void (async () => {
            await sender['_started'].promise;
            for (let i = 0; i < 100; i++) {
                if (sender['_ended']) return;
                sender.sendItem(i);
                await delay(5);
            }
            sender.sendEnd();
        })();

        const items: number[] = [];

        // Close the client peer after collecting some items — triggers disconnectAll
        setTimeout(() => clientPeer.close(), 30);

        await expect(async () => {
            for await (const item of stream) {
                items.push(item);
            }
        }).rejects.toThrow('Peer disconnected.');

        expect(items.length).toBeGreaterThan(0);
        expect(items.length).toBeLessThan(100);

        serverHub.close();
        clientHub.close();
    });

    it('should survive disconnect+reconnect when allowReconnect is true (end-to-end)', async () => {
        const serverHub = new RpcHub('server-hub');
        const clientHub = new RpcHub('client-hub');

        const [cc, sc] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(cc);
        clientHub.addPeer(clientPeer);

        const serverPeer = serverHub.getServerPeer('server://test');
        serverPeer.accept(sc);
        await delay(10);

        // Create sender with allowReconnect=true (default) on server, stream on client
        const sender = new RpcStreamSender<number>(serverPeer, 30, 61, true);
        serverPeer.sharedObjects.register(sender);
        const ref = parseStreamRef(sender.toRef())!;
        expect(ref.allowReconnect).toBe(true);

        const stream = new RpcStream<number>(ref, clientPeer);
        clientPeer.remoteObjects.register(stream);

        // Pump all items quickly and end
        void (async () => {
            await sender['_started'].promise;
            for (let i = 0; i < 5; i++) sender.sendItem(i);
            sender.sendEnd();
        })();

        const items: number[] = [];
        for await (const item of stream) {
            items.push(item);
        }

        // All items should arrive since we didn't disconnect
        expect(items).toEqual([0, 1, 2, 3, 4]);

        serverHub.close();
        clientHub.close();
    });
});

describe('RpcStreamSender direct', () => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let clientPeer: RpcClientPeer;
    let serverPeer: RpcServerPeer;

    beforeEach(async () => {
        serverHub = new RpcHub('server-hub');
        clientHub = new RpcHub('client-hub');

        const [cc, sc] = createMessageChannelPair();

        clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(cc);
        clientHub.addPeer(clientPeer);

        serverPeer = serverHub.getServerPeer('server://test');
        serverPeer.accept(sc);

        await delay(10);
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    it('should send batch of items', async () => {
        // Create sender on server side, register with server peer
        const sender = new RpcStreamSender<number>(serverPeer);
        serverPeer.sharedObjects.register(sender);
        const ref = parseStreamRef(sender.toRef())!;

        // Create consumer on client side
        const stream = new RpcStream<number>(ref, clientPeer);
        clientPeer.remoteObjects.register(stream);

        // Pump items via batch
        void (async () => {
            await sender['_started'].promise;
            sender.sendBatch([10, 20, 30]);
            sender.sendEnd();
        })();

        const items: number[] = [];
        for await (const item of stream) {
            items.push(item);
        }

        expect(items).toEqual([10, 20, 30]);
    });

    it('should allocate unique IDs for multiple senders', () => {
        const sender1 = new RpcStreamSender<number>(serverPeer);
        const sender2 = new RpcStreamSender<number>(serverPeer);

        expect(sender1.id.localId).not.toBe(sender2.id.localId);
    });

    it('should unregister on AckEnd', async () => {
        const sender = new RpcStreamSender<number>(serverPeer);
        serverPeer.sharedObjects.register(sender);

        expect(serverPeer.sharedObjects.get(sender.id.localId)).toBe(sender);

        sender.onAckEnd('test');

        expect(serverPeer.sharedObjects.get(sender.id.localId)).toBeUndefined();
    });
});
