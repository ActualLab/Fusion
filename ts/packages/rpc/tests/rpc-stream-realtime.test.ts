import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcHub,
    RpcClientPeer,
    RpcStream,
    RpcStreamSender,
    parseStreamRef,
    createMessageChannelPair,
    RpcWebSocketConnection,
    RpcSerializationFormat,
} from '../src/index.js';
import type { RpcServerPeer } from '../src/index.js';
import { delay } from './rpc-test-helpers.js';
import { createMockWsPair } from './mock-ws.js';

// -- parseStreamRef isRealTime tests --

describe('parseStreamRef isRealTime', () => {
    it('should parse isRealTime=true from 6th field', () => {
        const ref = parseStreamRef('abc,1,3,5,1,1');
        expect(ref).not.toBeNull();
        expect(ref!.isRealTime).toBe(true);
    });

    it('should parse isRealTime=false from 6th field', () => {
        const ref = parseStreamRef('abc,1,3,5,1,0');
        expect(ref).not.toBeNull();
        expect(ref!.isRealTime).toBe(false);
    });

    it('should default isRealTime to false when 6th field is missing', () => {
        const ref = parseStreamRef('abc,1,3,5,1');
        expect(ref).not.toBeNull();
        expect(ref!.isRealTime).toBe(false);
    });

    it('should default isRealTime to false when only 4 fields', () => {
        const ref = parseStreamRef('abc,1,3,5');
        expect(ref).not.toBeNull();
        expect(ref!.isRealTime).toBe(false);
    });

    it('should reject more than 6 fields', () => {
        expect(parseStreamRef('abc,1,3,5,1,1,extra')).toBeNull();
    });

    it('should parse isRealTime from binary format', () => {
        const ref = parseStreamRef({
            SerializedId: ['host-abc', 42],
            AckPeriod: 3,
            AckAdvance: 5,
            AllowReconnect: true,
            IsRealTime: true,
        });
        expect(ref).not.toBeNull();
        expect(ref!.isRealTime).toBe(true);
    });

    it('should default isRealTime to false in binary format', () => {
        const ref = parseStreamRef({
            SerializedId: ['host-abc', 42],
            AckPeriod: 3,
            AckAdvance: 5,
        });
        expect(ref).not.toBeNull();
        expect(ref!.isRealTime).toBe(false);
    });
});

// -- RpcStreamSender isRealTime + toRef --

describe('RpcStreamSender isRealTime', () => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let serverPeer: RpcServerPeer;

    beforeEach(async () => {
        serverHub = new RpcHub('server-hub');
        clientHub = new RpcHub('client-hub');

        const [cc, sc] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
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

    it('should include isRealTime=1 in toRef()', () => {
        const sender = new RpcStreamSender<number>(serverPeer, 3, 5, true, true);
        const ref = parseStreamRef(sender.toRef());
        expect(ref).not.toBeNull();
        expect(ref!.isRealTime).toBe(true);
        expect(ref!.ackPeriod).toBe(3);
        expect(ref!.ackAdvance).toBe(5);
    });

    it('should include isRealTime=0 in toRef() by default', () => {
        const sender = new RpcStreamSender<number>(serverPeer, 3, 5, true, false);
        const ref = parseStreamRef(sender.toRef());
        expect(ref).not.toBeNull();
        expect(ref!.isRealTime).toBe(false);
    });
});

// -- Helper: create paired sender + stream for real-time testing --

interface RealTimeTestSetup {
    serverHub: RpcHub;
    clientHub: RpcHub;
    clientPeer: RpcClientPeer;
    serverPeer: RpcServerPeer;
    createSender: (opts?: {
        ackPeriod?: number;
        ackAdvance?: number;
        isRealTime?: boolean;
        canSkipTo?: (item: number) => boolean;
    }) => {
        sender: RpcStreamSender<number>;
        stream: RpcStream<number>;
    };
}

function createRealTimeTestSetup(): RealTimeTestSetup {
    const serverHub = new RpcHub('server-hub');
    const clientHub = new RpcHub('client-hub');

    // Use WebSocket mock pair with default format.
    // The mock WS delivers via queueMicrotask, giving us async ACK delivery.
    const format = RpcSerializationFormat.get('json5np');
    const [clientWs, serverWs] = createMockWsPair();
    const clientConn = new RpcWebSocketConnection(clientWs, format.isBinary, format, clientHub.registry);
    const serverConn = new RpcWebSocketConnection(serverWs, format.isBinary, format, serverHub.registry);

    const clientPeer = new RpcClientPeer(clientHub, 'ws://test', 'json5np');
    clientPeer.connectWith(clientConn);
    clientHub.addPeer(clientPeer);

    const serverPeer = serverHub.getServerPeer('server://test');
    serverPeer.accept(serverConn);

    return {
        serverHub,
        clientHub,
        clientPeer,
        serverPeer,
        createSender(opts = {}) {
            const {
                ackPeriod = 3,
                ackAdvance = 5,
                isRealTime = true,
                canSkipTo = () => true,
            } = opts;
            const sender = new RpcStreamSender<number>(
                serverPeer, ackPeriod, ackAdvance, false, isRealTime, canSkipTo,
            );
            serverPeer.sharedObjects.register(sender);
            const ref = parseStreamRef(sender.toRef())!;
            const stream = new RpcStream<number>(ref, clientPeer);
            clientPeer.remoteObjects.register(stream);
            return { sender, stream };
        },
    };
}

// -- Real-time skip logic tests (TS sender) --
// These tests control ACK delivery directly rather than through E2E channels,
// because the mock WebSocket delivers ACKs via microtasks (near-instant RTT),
// making it impossible to build up a backlog at the sender.

describe.each([1, 2, 3, 5])('RpcStreamSender real-time skip (ackPeriod=%i)', (ackPeriod) => {
    let setup: RealTimeTestSetup;

    beforeEach(async () => {
        setup = createRealTimeTestSetup();
        await delay(10);
    });

    afterEach(() => {
        setup.serverHub.close();
        setup.clientHub.close();
    });

    it('should skip items when ACKs are delayed', async () => {
        const totalItems = 50;
        const ackAdvance = 5;

        const sender = new RpcStreamSender<number>(
            setup.serverPeer, ackPeriod, ackAdvance, false, true,
        );
        setup.serverPeer.sharedObjects.register(sender);

        // Track sent items
        const sentItems: number[] = [];
        const origSendItem = sender.sendItem.bind(sender);
        sender.sendItem = (item: number) => {
            sentItems.push(item);
            origSendItem(item);
        };

        // Source produces items (each yield goes through setTimeout)
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < totalItems; i++) {
                yield i;
                await new Promise(r => setTimeout(r, 0));
            }
        }

        // Simulate initial ACK to start the stream
        sender.onAck(0, sender.id.hostId);

        // Start writing — sender will block at ackAdvance ceiling
        const writeDone = sender.writeFrom(source());

        // Wait for sender to fill up to ackAdvance and block
        await delay(50);

        // Simulate delayed ACKs — only send a few, spaced out
        for (let ackIdx = 0; ackIdx < 5; ackIdx++) {
            sender.onAck(sentItems.length, '');
            await delay(100); // long delay between ACKs to simulate slow consumer
        }

        // Send final ACK to let stream finish
        await delay(50);
        sender.onAck(sentItems.length + ackAdvance, '');
        await writeDone;

        // Should have sent fewer items than total (skipping occurred)
        expect(sentItems.length).toBeLessThan(totalItems);
        expect(sentItems.length).toBeGreaterThan(0);
        // First item should be 0
        expect(sentItems[0]).toBe(0);
        // Items should be in ascending order
        for (let i = 1; i < sentItems.length; i++) {
            expect(sentItems[i]).toBeGreaterThan(sentItems[i - 1]);
        }
    });

    it('should not skip when ACKs arrive promptly', async () => {
        const totalItems = 20;
        const ackAdvance = 10;

        const { sender, stream } = setup.createSender({ ackPeriod, ackAdvance, isRealTime: true });

        // Source: produces items with delay (slow source, fast consumer)
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < totalItems; i++) {
                yield i;
                await delay(5);
            }
        }

        void sender.writeFrom(source());

        const received: number[] = [];
        for await (const item of stream) {
            received.push(item);
        }

        // Fast consumer should receive all items
        expect(received).toEqual(Array.from({ length: totalItems }, (_, i) => i));
    });

    it('should skip to keyframes when canSkipTo filters', async () => {
        const totalItems = 100;
        const ackAdvance = 5;
        const keyFrameInterval = 10;

        const sender = new RpcStreamSender<number>(
            setup.serverPeer, ackPeriod, ackAdvance, false, true,
            (item) => item % keyFrameInterval === 0,
        );
        setup.serverPeer.sharedObjects.register(sender);

        const sentItems: number[] = [];
        const origSendItem = sender.sendItem.bind(sender);
        sender.sendItem = (item: number) => {
            sentItems.push(item);
            origSendItem(item);
        };

        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < totalItems; i++) {
                yield i;
                await new Promise(r => setTimeout(r, 0));
            }
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());

        // Simulate delayed ACKs
        for (let ackIdx = 0; ackIdx < 5; ackIdx++) {
            await delay(100);
            sender.onAck(sentItems.length, '');
        }

        await delay(50);
        sender.onAck(sentItems.length + ackAdvance, '');
        await writeDone;

        // Should have skipped some items
        expect(sentItems.length).toBeLessThan(totalItems);
        expect(sentItems.length).toBeGreaterThan(0);

        // Items should be in ascending order
        for (let i = 1; i < sentItems.length; i++) {
            expect(sentItems[i]).toBeGreaterThan(sentItems[i - 1]);
        }

        // After skipping, there should be gaps in the sequence
        const gaps: number[] = [];
        for (let i = 1; i < sentItems.length; i++) {
            if (sentItems[i] > sentItems[i - 1] + 1) {
                gaps.push(sentItems[i]);
            }
        }
        expect(gaps.length).toBeGreaterThan(0);
    });
});

// -- Real-time reconnect tests --
// When a reset ACK arrives on a real-time stream, the sender should
// skip to the next canSkipTo item.
// We use a very large ackAdvance so backpressure never triggers — all gaps
// are exclusively from reconnect skipping.

describe.each([1, 3, 5])('RpcStreamSender real-time reconnect (ackPeriod=%i)', (ackPeriod) => {
    let setup: RealTimeTestSetup;

    beforeEach(async () => {
        setup = createRealTimeTestSetup();
        await delay(10);
    });

    afterEach(() => {
        setup.serverHub.close();
        setup.clientHub.close();
    });

    it('should skip to keyframe on reconnect with canSkipTo filter', async () => {
        const totalItems = 200;
        const ackAdvance = totalItems; // No backpressure — isolates reconnect behavior
        const keyFrameInterval = 10;

        const sender = new RpcStreamSender<number>(
            setup.serverPeer, ackPeriod, ackAdvance, true, true,
            (item) => item % keyFrameInterval === 0,
        );
        setup.serverPeer.sharedObjects.register(sender);

        const sentItems: number[] = [];
        const origSendItem = sender.sendItem.bind(sender);
        sender.sendItem = (item: number) => {
            sentItems.push(item);
            origSendItem(item);
        };

        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < totalItems; i++) {
                yield i;
                await new Promise(r => setTimeout(r, 1));
            }
        }

        // Start the stream
        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());

        // Wait until enough items are sent (past a keyframe boundary)
        while (sentItems.length < 13) await delay(5);

        // Simulate reconnect: send a reset ACK with hostId
        const countBefore = sentItems.length;
        sender.onAck(countBefore, sender.id.hostId);

        // Wait for stream to complete
        await writeDone;

        // Items should be in ascending order
        for (let i = 1; i < sentItems.length; i++) {
            expect(sentItems[i]).toBeGreaterThan(sentItems[i - 1]);
        }

        // Find gaps
        const gaps: { before: number; after: number }[] = [];
        for (let i = 1; i < sentItems.length; i++) {
            if (sentItems[i] > sentItems[i - 1] + 1) {
                gaps.push({ before: sentItems[i - 1], after: sentItems[i] });
            }
        }

        // Should have at least one gap (the reconnect skip)
        expect(gaps.length).toBeGreaterThanOrEqual(1);

        // Every item after a gap should be a keyframe (multiple of keyFrameInterval)
        for (const gap of gaps) {
            expect(gap.after % keyFrameInterval).toBe(0);
        }
    });

    it('should resume immediately on reconnect when canSkipTo is default', async () => {
        const totalItems = 100;
        const ackAdvance = totalItems;

        const sender = new RpcStreamSender<number>(
            setup.serverPeer, ackPeriod, ackAdvance, true, true,
        );
        setup.serverPeer.sharedObjects.register(sender);

        const sentItems: number[] = [];
        const origSendItem = sender.sendItem.bind(sender);
        sender.sendItem = (item: number) => {
            sentItems.push(item);
            origSendItem(item);
        };

        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < totalItems; i++) {
                yield i;
                await new Promise(r => setTimeout(r, 1));
            }
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());

        // Wait for items, then reconnect
        while (sentItems.length < 15) await delay(5);
        sender.onAck(sentItems.length, sender.id.hostId);

        await writeDone;

        // Items should be in ascending order
        for (let i = 1; i < sentItems.length; i++) {
            expect(sentItems[i]).toBeGreaterThan(sentItems[i - 1]);
        }

        // With default canSkipTo (all true), the very next item is accepted,
        // so there may be at most a gap of 1 (the item consumed during reset check).
        // All items should still be accounted for.
        expect(sentItems.length).toBeGreaterThan(15);
    });
});

// -- Non-real-time flow control (back-pressure) --

describe('RpcStreamSender flow control (non-real-time)', () => {
    let setup: RealTimeTestSetup;

    beforeEach(async () => {
        setup = createRealTimeTestSetup();
        await delay(10);
    });

    afterEach(() => {
        setup.serverHub.close();
        setup.clientHub.close();
    });

    it('should deliver all items with slow consumer (back-pressure, no skipping)', async () => {
        const totalItems = 20;

        const { sender, stream } = setup.createSender({
            ackPeriod: 3,
            ackAdvance: 5,
            isRealTime: false,
        });

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < totalItems; i++) {
                yield i;
            }
        }

        void sender.writeFrom(source());

        const received: number[] = [];
        for await (const item of stream) {
            received.push(item);
            await delay(5);
        }

        // All items should be received — back-pressure, no skipping
        expect(received).toEqual(Array.from({ length: totalItems }, (_, i) => i));
    });
});

// -- .NET-TS E2E wire format compatibility --

describe('RpcStream real-time wire format E2E', () => {
    it('should round-trip isRealTime through text serialization', () => {
        // Simulate .NET server producing a ref string with isRealTime=1
        const dotnetRef = '550e8400-e29b-41d4-a716-446655440000,42,3,5,1,1';
        const parsed = parseStreamRef(dotnetRef);
        expect(parsed).not.toBeNull();
        expect(parsed!.ackPeriod).toBe(3);
        expect(parsed!.ackAdvance).toBe(5);
        expect(parsed!.allowReconnect).toBe(true);
        expect(parsed!.isRealTime).toBe(true);

        // Simulate TS sender producing a ref string
        const serverHub = new RpcHub('server-hub');
        const [, sc] = createMessageChannelPair();
        const serverPeer = serverHub.getServerPeer('server://test');
        serverPeer.accept(sc);

        const sender = new RpcStreamSender<number>(serverPeer, 3, 5, true, true);
        const tsRef = sender.toRef();
        const reParsed = parseStreamRef(tsRef);
        expect(reParsed).not.toBeNull();
        expect(reParsed!.ackPeriod).toBe(3);
        expect(reParsed!.ackAdvance).toBe(5);
        expect(reParsed!.isRealTime).toBe(true);

        serverHub.close();
    });

    it('should handle old .NET format without isRealTime field', () => {
        // Old .NET server sends 5-field format
        const oldRef = '550e8400-e29b-41d4-a716-446655440000,42,30,61,1';
        const parsed = parseStreamRef(oldRef);
        expect(parsed).not.toBeNull();
        expect(parsed!.isRealTime).toBe(false); // backward compat default
    });

    it('should handle 4-field format (very old)', () => {
        const veryOldRef = '550e8400-e29b-41d4-a716-446655440000,42,30,61';
        const parsed = parseStreamRef(veryOldRef);
        expect(parsed).not.toBeNull();
        expect(parsed!.allowReconnect).toBe(true);
        expect(parsed!.isRealTime).toBe(false);
    });
});
