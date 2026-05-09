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

        // Tear the sender down to make `writeDone` resolve. The previous
        // approach (final `onAck(sentItems.length + ackAdvance, '')` then
        // `await writeDone`) relied on the source naturally exhausting
        // within the given budget — which depends on the OS-level
        // setTimeout(0) granularity and is too tight on Windows (~15ms
        // vs ~1ms on Linux). The invariants we actually want to assert
        // (skipping happened, items are ordered, first item is 0) are
        // independent of whether the source ran to completion.
        await delay(50);
        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });

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
        // Keep keyframes close enough to fit into the already-buffered
        // unsent suffix. Real-time streams no longer pull ahead just to
        // discover a future skip target.
        const keyFrameInterval = 6;

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

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < totalItems; i++) {
                yield i;
            }
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());

        // Simulate delayed ACKs
        for (let ackIdx = 0; ackIdx < 5; ackIdx++) {
            await delay(100);
            sender.onAck(sentItems.length, '');
        }

        // Force completion (see the equivalent comment in
        // `should skip items when ACKs are delayed` above for why we
        // disconnect rather than wait for natural source exhaustion).
        await delay(50);
        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });

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

describe('RpcStreamSender backpressure modes', () => {
    let setup: RealTimeTestSetup;

    beforeEach(async () => {
        setup = createRealTimeTestSetup();
        await delay(10);
    });

    afterEach(() => {
        setup.serverHub.close();
        setup.clientHub.close();
    });

    it('should pause a non-real-time source at the ACK window', async () => {
        const ackAdvance = 5;
        let producedCount = 0;
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 3, ackAdvance, false, false, (item) => item % 10 === 0,
        );
        setup.serverPeer.sharedObjects.register(sender);

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 100; i++) {
                producedCount++;
                yield i;
            }
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());
        await delay(0);

        expect(producedCount).toBeLessThanOrEqual(ackAdvance);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('should not pull past the buffer without a real-time skip target', async () => {
        const ackAdvance = 5;
        const keyFrameInterval = 10;
        let producedCount = 0;
        const sentItems: number[] = [];
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 3, ackAdvance, false, true, (item) => item % keyFrameInterval === 0,
        );
        setup.serverPeer.sharedObjects.register(sender);
        const origSendItem = sender.sendItem.bind(sender);
        sender.sendItem = (item: number) => {
            sentItems.push(item);
            origSendItem(item);
        };

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 100; i++) {
                producedCount++;
                yield i;
            }
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());
        await delay(0);

        expect(producedCount).toBeLessThanOrEqual(ackAdvance + 2);
        expect(sentItems[0]).toBe(0);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('should start from a buffered real-time skip target', async () => {
        const ackAdvance = 15;
        const keyFrameInterval = 8;
        const expectedSkipTarget = keyFrameInterval * 2;
        const sentItems: number[] = [];
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 3, ackAdvance, false, true, (item) => item % keyFrameInterval === 0,
        );
        setup.serverPeer.sharedObjects.register(sender);
        const origSendItem = sender.sendItem.bind(sender);
        sender.sendItem = (item: number) => {
            sentItems.push(item);
            origSendItem(item);
        };

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 100; i++)
                yield i;
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());
        for (let i = 0; sentItems.length < ackAdvance && i < 100; i++)
            await delay(0);
        expect(sentItems).toEqual(Array.from({ length: ackAdvance }, (_, i) => i));

        sender.onAck(1, '');
        for (let i = 0; !sentItems.includes(expectedSkipTarget) && i < 100; i++)
            await delay(0);

        expect(sentItems).not.toContain(expectedSkipTarget - 1);
        expect(sentItems).toContain(expectedSkipTarget);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });
});

// -- Observability metrics: nextIndex, lastAckIndex, skipCount, onAckProcessed --

describe('RpcStreamSender observability metrics', () => {
    let setup: RealTimeTestSetup;

    beforeEach(async () => {
        setup = createRealTimeTestSetup();
        await delay(10);
    });

    afterEach(() => {
        setup.serverHub.close();
        setup.clientHub.close();
    });

    it('nextIndex and lastAckIndex track sends and ACKs', async () => {
        const ackAdvance = 5;
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 1, ackAdvance, false, false,
        );
        setup.serverPeer.sharedObjects.register(sender);

        expect(sender.nextIndex).toBe(0);
        expect(sender.lastAckIndex).toBe(0);

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 100; i++) yield i;
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());

        // Initial ACK opens a window of `ackAdvance`; sender advances nextIndex
        // by exactly that many before stalling on the next ACK.
        for (let i = 0; sender.nextIndex < ackAdvance && i < 100; i++)
            await delay(0);
        expect(sender.nextIndex).toBe(ackAdvance);
        expect(sender.lastAckIndex).toBe(0);

        // Acknowledge a few items: lastAckIndex follows; the window grows by
        // the same delta and nextIndex catches up.
        sender.onAck(3, '');
        for (let i = 0; sender.lastAckIndex < 3 && i < 100; i++) await delay(0);
        expect(sender.lastAckIndex).toBe(3);
        for (let i = 0; sender.nextIndex < 3 + ackAdvance && i < 100; i++)
            await delay(0);
        expect(sender.nextIndex).toBe(3 + ackAdvance);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('lastAckIndex reflects only the most recent ACK in a coalesced drain', async () => {
        const ackAdvance = 100;
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 1, ackAdvance, false, false,
        );
        setup.serverPeer.sharedObjects.register(sender);

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 1000; i++) yield i;
        }

        // Queue three ACKs synchronously before the pump starts so they
        // collapse into a single drain.
        sender.onAck(0, sender.id.hostId);
        sender.onAck(5, '');
        sender.onAck(10, '');

        const writeDone = sender.writeFrom(source());
        for (let i = 0; sender.lastAckIndex < 10 && i < 100; i++)
            await delay(0);
        expect(sender.lastAckIndex).toBe(10);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('onAckProcessed fires once per ACK drain', async () => {
        const ackAdvance = 5;
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 1, ackAdvance, false, false,
        );
        setup.serverPeer.sharedObjects.register(sender);

        let callbackCount = 0;
        sender.onAckProcessed = () => { callbackCount++; };

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 1000; i++) yield i;
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());

        // Initial ACK(0) drained once writeFrom runs.
        for (let i = 0; callbackCount < 1 && i < 100; i++) await delay(0);
        expect(callbackCount).toBe(1);

        // Three subsequent ACKs, each awaited so they each produce a separate
        // drain and a separate callback.
        for (let n = 1; n <= 3; n++) {
            sender.onAck(n * ackAdvance, '');
            for (let i = 0; sender.lastAckIndex < n * ackAdvance && i < 100; i++)
                await delay(0);
            expect(sender.lastAckIndex).toBe(n * ackAdvance);
        }
        expect(callbackCount).toBe(4);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('onAckProcessed fires exactly once when multiple ACKs are coalesced', async () => {
        const ackAdvance = 100;
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 1, ackAdvance, false, false,
        );
        setup.serverPeer.sharedObjects.register(sender);

        let callbackCount = 0;
        sender.onAckProcessed = () => { callbackCount++; };

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 1000; i++) yield i;
        }

        // Three queued ACKs before the pump starts → single drain at startup.
        sender.onAck(0, sender.id.hostId);
        sender.onAck(5, '');
        sender.onAck(10, '');

        const writeDone = sender.writeFrom(source());
        for (let i = 0; sender.lastAckIndex < 10 && i < 100; i++)
            await delay(0);
        expect(sender.lastAckIndex).toBe(10);
        expect(callbackCount).toBe(1);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('onBuffered reports buffer count after each push', async () => {
        const ackAdvance = 5;
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 1, ackAdvance, false, false,
        );
        setup.serverPeer.sharedObjects.register(sender);

        const counts: number[] = [];
        sender.onBuffered = (n) => { counts.push(n); };

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 1000; i++) yield i;
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());
        // Wait for the sender to finish prebuffering its initial window.
        for (let i = 0; counts.length === 0 && i < 100; i++) await delay(0);
        await delay(0);

        // Counts should be strictly increasing: each push extends the buffer
        // by one item, so we observe 1, 2, 3, ... up to (at least) ackAdvance.
        expect(counts.length).toBeGreaterThanOrEqual(ackAdvance);
        for (let i = 0; i < counts.length; i++)
            expect(counts[i]).toBe(i + 1);

        // The buffer never grows past sender.bufferSize.
        for (const n of counts)
            expect(n).toBeLessThanOrEqual(sender.bufferSize);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('onBuffered + onAckProcessed together expose buffer utilization', async () => {
        // onBuffered marks every item *entering* the buffer; onAckProcessed
        // marks every batch leaving via ACK. With those two, controllers can
        // detect "buffer full → source pull is paused" by comparing the last
        // bufferedCount against sender.bufferSize.
        const ackAdvance = 5;
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 1, ackAdvance, false, false,
        );
        setup.serverPeer.sharedObjects.register(sender);

        let lastBuffered = 0;
        sender.onBuffered = (n) => { lastBuffered = n; };

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 1000; i++) yield i;
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());
        for (let i = 0; lastBuffered < sender.bufferSize && i < 100; i++)
            await delay(0);

        // Source pull is paused — buffer reached the configured size.
        expect(lastBuffered).toBe(sender.bufferSize);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('onBuffered swallows listener errors', async () => {
        const ackAdvance = 5;
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 1, ackAdvance, false, false,
        );
        setup.serverPeer.sharedObjects.register(sender);

        let callCount = 0;
        sender.onBuffered = () => {
            callCount++;
            throw new Error('listener boom');
        };

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 1000; i++) yield i;
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());
        for (let i = 0; callCount < 1 && i < 100; i++) await delay(0);

        // The pump survived the throwing listener and processed at least one
        // ACK after it (sender.lastAckIndex was advanced from 0 → ackAdvance).
        sender.onAck(ackAdvance, '');
        for (let i = 0; sender.lastAckIndex < ackAdvance && i < 100; i++)
            await delay(0);
        expect(callCount).toBeGreaterThan(0);
        expect(sender.lastAckIndex).toBe(ackAdvance);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('onAckProcessed swallows listener errors', async () => {
        const ackAdvance = 5;
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 1, ackAdvance, false, false,
        );
        setup.serverPeer.sharedObjects.register(sender);

        let callCount = 0;
        sender.onAckProcessed = () => {
            callCount++;
            throw new Error('listener boom');
        };

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 1000; i++) yield i;
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());
        for (let i = 0; callCount < 1 && i < 100; i++) await delay(0);

        // A second ACK proves the pump survived the throwing listener.
        sender.onAck(ackAdvance, '');
        for (let i = 0; sender.lastAckIndex < ackAdvance && i < 100; i++)
            await delay(0);
        expect(callCount).toBe(2);
        expect(sender.lastAckIndex).toBe(ackAdvance);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
    });

    it('skipCount stays 0 for non-real-time streams', async () => {
        const totalItems = 20;

        const { sender, stream } = setup.createSender({
            ackPeriod: 3,
            ackAdvance: 5,
            isRealTime: false,
        });

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < totalItems; i++) yield i;
        }

        void sender.writeFrom(source());

        const received: number[] = [];
        for await (const item of stream) {
            received.push(item);
            await delay(5);
        }

        expect(received.length).toBe(totalItems);
        expect(sender.skipCount).toBe(0);
    });

    it('skipCount equals the number of items dropped during real-time compaction', async () => {
        // Same setup as "should start from a buffered real-time skip target":
        // ackAdvance=15, keyframes every 8. After the initial fill of [0..14]
        // and ACK(1), the pump pulls items 15 and 16, then collapses the
        // unsent suffix to the latest keyframe (16), dropping the lone
        // non-keyframe (item 15). Exactly one skip.
        const ackAdvance = 15;
        const keyFrameInterval = 8;
        const expectedSkipTarget = keyFrameInterval * 2; // 16
        const sentItems: number[] = [];
        const sender = new RpcStreamSender<number>(
            setup.serverPeer, 3, ackAdvance, false, true,
            (item) => item % keyFrameInterval === 0,
        );
        setup.serverPeer.sharedObjects.register(sender);
        const origSendItem = sender.sendItem.bind(sender);
        sender.sendItem = (item: number) => {
            sentItems.push(item);
            origSendItem(item);
        };

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source(): AsyncGenerator<number> {
            for (let i = 0; i < 100; i++) yield i;
        }

        sender.onAck(0, sender.id.hostId);
        const writeDone = sender.writeFrom(source());

        for (let i = 0; sentItems.length < ackAdvance && i < 100; i++)
            await delay(0);
        expect(sentItems).toEqual(Array.from({ length: ackAdvance }, (_, i) => i));
        expect(sender.skipCount).toBe(0);

        sender.onAck(1, '');
        for (let i = 0; !sentItems.includes(expectedSkipTarget) && i < 100; i++)
            await delay(0);

        expect(sentItems).toContain(expectedSkipTarget);
        expect(sentItems).not.toContain(expectedSkipTarget - 1);
        expect(sender.skipCount).toBe(1);

        sender.disconnect();
        await writeDone.catch(() => { /* noop */ });
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
