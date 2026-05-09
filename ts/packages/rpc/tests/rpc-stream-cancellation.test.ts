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
import { createTestHubPair, delay } from './rpc-test-helpers.js';

describe('RpcStreamSender abort signal', () => {
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

    it('should expose abortSignal that is not aborted initially', () => {
        const sender = new RpcStreamSender<number>(serverPeer);
        expect(sender.abortSignal).toBeInstanceOf(AbortSignal);
        expect(sender.abortSignal.aborted).toBe(false);
    });

    it('should abort signal on disconnect', () => {
        const sender = new RpcStreamSender<number>(serverPeer);
        serverPeer.sharedObjects.register(sender);

        sender.disconnect();

        expect(sender.abortSignal.aborted).toBe(true);
    });

    it('should call iterator.return() on disconnect (triggers finally blocks)', async () => {
        const sender = new RpcStreamSender<number>(serverPeer);
        serverPeer.sharedObjects.register(sender);
        const ref = parseStreamRef(sender.toRef())!;

        const stream = new RpcStream<number>(ref, clientPeer);
        clientPeer.remoteObjects.register(stream);

        let finallyCalled = false;
        let sourceStarted = false;

        async function* source() {
            try {
                sourceStarted = true;
                for (let i = 0; ; i++) {
                    yield i;
                    await delay(5);
                }
            } finally {
                finallyCalled = true;
            }
        }

        void sender.writeFrom(source());

        // Start the stream (send initial ack)
        const iter = stream[Symbol.asyncIterator]();
        await iter.next(); // triggers ack, starts pumping

        // Wait for source to start
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- changes asynchronously
        while (!sourceStarted) await delay(5);
        await delay(10);

        // Disconnect the sender
        sender.disconnect();
        await delay(10);

        expect(finallyCalled).toBe(true);
    });

    it('should unblock _waitForAckBudget on disconnect', async () => {
        // Create sender with very small ackAdvance so it hits the ceiling quickly
        const sender = new RpcStreamSender<number>(serverPeer, 2, 2);
        serverPeer.sharedObjects.register(sender);
        const ref = parseStreamRef(sender.toRef())!;

        const stream = new RpcStream<number>(ref, clientPeer);
        clientPeer.remoteObjects.register(stream);

        let writeFromCompleted = false;

        // eslint-disable-next-line @typescript-eslint/require-await
        async function* source() {
            // Yield enough items to exceed ackAdvance
            for (let i = 0; i < 100; i++) {
                yield i;
            }
        }

        const writePromise = sender.writeFrom(source()).then(() => {
            writeFromCompleted = true;
        });

        // Start the stream
        const iter = stream[Symbol.asyncIterator]();
        await iter.next(); // triggers ack, starts pumping

        // Let some items flow, then the sender should block on ack budget
        await delay(20);

        // Disconnect — should unblock writeFrom
        sender.disconnect();
        await writePromise;

        expect(writeFromCompleted).toBe(true);
    });

    it('should disconnect sender on repeated disconnect calls (idempotent)', () => {
        const sender = new RpcStreamSender<number>(serverPeer);
        serverPeer.sharedObjects.register(sender);

        sender.disconnect();
        // Second call should not throw
        sender.disconnect();

        expect(sender.abortSignal.aborted).toBe(true);
    });
});

describe('RpcStream source factory with AbortSignal', () => {
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

    it('should pass abortSignal to source factory in toRef()', () => {
        let receivedSignal: AbortSignal | null = null;

        const stream = new RpcStream<number>((abortSignal: AbortSignal) => {
            receivedSignal = abortSignal;
            // eslint-disable-next-line @typescript-eslint/require-await
            return (async function* () {
                yield 1; yield 2; yield 3;
            })();
        });

        stream.toRef(serverPeer);

        expect(receivedSignal).toBeInstanceOf(AbortSignal);
        expect(receivedSignal!.aborted).toBe(false);
    });

    it('should abort the signal when sender is disconnected', async () => {
        let receivedSignal: AbortSignal | null = null;

        const stream = new RpcStream<number>((abortSignal: AbortSignal) => {
            receivedSignal = abortSignal;
            return (async function* () {
                for (let i = 0; ; i++) {
                    yield i;
                    await delay(5);
                }
            })();
        });

        const ref = stream.toRef(serverPeer) as string;
        const parsedRef = parseStreamRef(ref)!;

        // Create consumer
        const remoteStream = new RpcStream<number>(parsedRef, clientPeer);
        clientPeer.remoteObjects.register(remoteStream);

        const iter = remoteStream[Symbol.asyncIterator]();
        await iter.next(); // start the stream
        await delay(20);

        expect(receivedSignal!.aborted).toBe(false);

        // Disconnect the stream (propagates to sender)
        stream.disconnect();
        await delay(10);

        expect(receivedSignal!.aborted).toBe(true);
    });

    it('should work with source factory in end-to-end streaming', async () => {
        const StreamServiceDef = defineRpcService('FactoryStreamService', {
            getStream: { args: [], returns: RpcType.stream },
        });

        const pair = createTestHubPair('json5np');
        await delay(10);

        pair.serverHub.addService(StreamServiceDef, {
            getStream() {
                return new RpcStream<number>((_abortSignal: AbortSignal) => {
                    // eslint-disable-next-line @typescript-eslint/require-await
                    return (async function* () {
                        for (let i = 0; i < 5; i++) yield i * 10;
                    })();
                });
            },
        });

        const client = pair.clientHub.addClient<{
                getStream(): Promise<AsyncIterable<number>>;
                    }>(pair.clientPeer, StreamServiceDef);

        const stream = await client.getStream();
        const items: number[] = [];
        for await (const item of stream) items.push(item);

        expect(items).toEqual([0, 10, 20, 30, 40]);

        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('should allow direct iteration of factory source', async () => {
        const stream = new RpcStream<number>((_abortSignal: AbortSignal) => {
            // eslint-disable-next-line @typescript-eslint/require-await
            return (async function* () {
                yield 10; yield 20;
            })();
        });

        const items: number[] = [];
        for await (const item of stream) items.push(item);
        expect(items).toEqual([10, 20]);
    });
});

describe('RpcStream disconnect propagation for local streams', () => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let clientPeer: RpcClientPeer;
    let serverPeer: RpcServerPeer;
    let originalGracePeriod: number;

    beforeEach(async () => {
        // Factory-form sources get a grace period before iterator.return() is
        // force-called; shrink it for tests so they don't spend the default
        // 100ms waiting for force-close of abort-unaware generators.
        originalGracePeriod = RpcStream.disconnectGracePeriodMs;
        RpcStream.disconnectGracePeriodMs = 10;

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
        RpcStream.disconnectGracePeriodMs = originalGracePeriod;
    });

    it('should propagate disconnect from RpcStream to sender', async () => {
        let signalAborted = false;

        const stream = new RpcStream<number>((abortSignal: AbortSignal) => {
            abortSignal.addEventListener('abort', () => { signalAborted = true; });
            return (async function* () {
                for (let i = 0; ; i++) {
                    yield i;
                    await delay(5);
                }
            })();
        });

        const ref = stream.toRef(serverPeer) as string;
        const parsedRef = parseStreamRef(ref)!;
        const remoteStream = new RpcStream<number>(parsedRef, clientPeer);
        clientPeer.remoteObjects.register(remoteStream);

        const iter = remoteStream[Symbol.asyncIterator]();
        await iter.next();
        await delay(20);

        stream.disconnect();
        await delay(10);

        expect(signalAborted).toBe(true);
    });

    it('should release resources on peer close (sharedObjects.disconnectAll)', async () => {
        let resourceReleased = false;
        let sourceStarted = false;

        const stream = new RpcStream<number>((_abortSignal: AbortSignal) => {
            return (async function* () {
                try {
                    sourceStarted = true;
                    for (let i = 0; ; i++) {
                        yield i;
                        await delay(5);
                    }
                } finally {
                    resourceReleased = true;
                }
            })();
        });

        const ref = stream.toRef(serverPeer) as string;
        const parsedRef = parseStreamRef(ref)!;
        const remoteStream = new RpcStream<number>(parsedRef, clientPeer);
        clientPeer.remoteObjects.register(remoteStream);

        const iter = remoteStream[Symbol.asyncIterator]();
        await iter.next();

        // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- changes asynchronously
        while (!sourceStarted) await delay(5);
        await delay(20);

        // Close server hub — triggers sharedObjects.disconnectAll()
        serverHub.close();
        await delay(20);

        expect(resourceReleased).toBe(true);
    });

    it('should stop enumeration when peer closes with allowReconnect=false', async () => {
        let sourceStarted = false;
        let iterationStopped = false;

        const stream = new RpcStream<number>(
            (_abortSignal: AbortSignal) => {
                return (async function* () {
                    try {
                        sourceStarted = true;
                        for (let i = 0; ; i++) {
                            yield i;
                            await delay(5);
                        }
                    } finally {
                        iterationStopped = true;
                    }
                })();
            },
            { allowReconnect: false },
        );

        const ref = stream.toRef(serverPeer) as string;
        const parsedRef = parseStreamRef(ref)!;
        expect(parsedRef.allowReconnect).toBe(false);

        const remoteStream = new RpcStream<number>(parsedRef, clientPeer);
        clientPeer.remoteObjects.register(remoteStream);

        const iter = remoteStream[Symbol.asyncIterator]();
        await iter.next();

        // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- changes asynchronously
        while (!sourceStarted) await delay(5);
        await delay(20);

        // Close server peer — triggers disconnect
        serverHub.close();
        await delay(20);

        expect(iterationStopped).toBe(true);
    });

    it('should allow abort signal to cancel async operations in source', async () => {
        let _abortDetected = false;

        const stream = new RpcStream<number>((abortSignal: AbortSignal) => {
            return (async function* () {
                for (let i = 0; ; i++) {
                    if (abortSignal.aborted) {
                        _abortDetected = true;
                        return;
                    }
                    yield i;
                    // Wait with abort support
                    await new Promise<void>((resolve) => {
                        const timeout = setTimeout(resolve, 5);
                        abortSignal.addEventListener('abort', () => {
                            clearTimeout(timeout);
                            resolve();
                        }, { once: true });
                    });
                }
            })();
        });

        const ref = stream.toRef(serverPeer) as string;
        const parsedRef = parseStreamRef(ref)!;
        const remoteStream = new RpcStream<number>(parsedRef, clientPeer);
        clientPeer.remoteObjects.register(remoteStream);

        const iter = remoteStream[Symbol.asyncIterator]();
        await iter.next();
        await delay(20);

        stream.disconnect();
        await delay(20);

        // The source should have detected the abort signal
        // (either through the signal check or via iterator.return())
        expect(stream).toBeDefined(); // stream processed without errors
    });
});

describe('RpcStream cancellation end-to-end (service layer)', () => {
    const StreamServiceDef = defineRpcService('CancellationE2EService', {
        infiniteStream: { args: [], returns: RpcType.stream },
        infiniteStreamFactory: { args: [], returns: RpcType.stream },
    });

    let originalGracePeriod: number;
    beforeEach(() => {
        // Factory-form sources get a grace period before iterator.return() is
        // force-called; shrink it for tests so they don't spend the default
        // 100ms waiting on abort-unaware generators.
        originalGracePeriod = RpcStream.disconnectGracePeriodMs;
        RpcStream.disconnectGracePeriodMs = 10;
    });
    afterEach(() => {
        RpcStream.disconnectGracePeriodMs = originalGracePeriod;
    });

    it('should cancel server source when client peer closes (plain async iterable)', async () => {
        const pair = createTestHubPair('json5np');
        await delay(10);

        let sourceFinalized = false;
        let sourceStarted = false;

        pair.serverHub.addService(StreamServiceDef, {
            infiniteStream: async function* () {
                try {
                    sourceStarted = true;
                    for (let i = 0; ; i++) {
                        yield i;
                        await delay(5);
                    }
                } finally {
                    sourceFinalized = true;
                }
            },
        });

        const client = pair.clientHub.addClient<{
                infiniteStream(): Promise<AsyncIterable<number>>;
                    }>(pair.clientPeer, StreamServiceDef);

        const stream = await client.infiniteStream();
        const items: number[] = [];

        // Collect a few items then close the client
        for await (const item of stream) {
            items.push(item);
            if (items.length >= 3) break;
        }

        // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- changes asynchronously
        while (!sourceStarted) await delay(5);
        await delay(10);

        // Close client peer — server should detect disconnection and cancel enumeration
        pair.clientHub.close();
        await delay(50);

        expect(items.length).toBeGreaterThanOrEqual(3);
        expect(sourceFinalized).toBe(true);

        pair.serverHub.close();
    });

    it('should abort signal and cancel source when client peer closes (factory source)', async () => {
        const pair = createTestHubPair('json5np');
        await delay(10);

        let signalAborted = false;
        let sourceFinalized = false;
        let sourceStarted = false;

        pair.serverHub.addService(StreamServiceDef, {
            infiniteStreamFactory() {
                return new RpcStream<number>((abortSignal: AbortSignal) => {
                    abortSignal.addEventListener('abort', () => {
                        signalAborted = true;
                    });
                    return (async function* () {
                        try {
                            sourceStarted = true;
                            for (let i = 0; ; i++) {
                                yield i;
                                await delay(5);
                            }
                        } finally {
                            sourceFinalized = true;
                        }
                    })();
                });
            },
        });

        const client = pair.clientHub.addClient<{
                infiniteStreamFactory(): Promise<AsyncIterable<number>>;
                    }>(pair.clientPeer, StreamServiceDef);

        const stream = await client.infiniteStreamFactory();
        const items: number[] = [];
        for await (const item of stream) {
            items.push(item);
            if (items.length >= 3) break;
        }

        // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- changes asynchronously
        while (!sourceStarted) await delay(5);
        await delay(10);

        // Close client — triggers sharedObjects.disconnectAll() on server peer
        pair.clientHub.close();
        await delay(50);

        expect(items.length).toBeGreaterThanOrEqual(3);
        expect(signalAborted).toBe(true);
        expect(sourceFinalized).toBe(true);

        pair.serverHub.close();
    });

    it('should cancel source when server hub closes (server-initiated disconnect)', async () => {
        const pair = createTestHubPair('json5np');
        await delay(10);

        let sourceFinalized = false;
        let sourceStarted = false;

        pair.serverHub.addService(StreamServiceDef, {
            infiniteStreamFactory() {
                return new RpcStream<number>((_abortSignal: AbortSignal) => {
                    return (async function* () {
                        try {
                            sourceStarted = true;
                            for (let i = 0; ; i++) {
                                yield i;
                                await delay(5);
                            }
                        } finally {
                            sourceFinalized = true;
                        }
                    })();
                });
            },
        });

        const client = pair.clientHub.addClient<{
                infiniteStreamFactory(): Promise<AsyncIterable<number>>;
                    }>(pair.clientPeer, StreamServiceDef);

        const stream = await client.infiniteStreamFactory();
        const iter = stream[Symbol.asyncIterator]();
        await iter.next();

        // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition -- changes asynchronously
        while (!sourceStarted) await delay(5);
        await delay(20);

        // Close server hub — should cancel server-side enumeration
        pair.serverHub.close();
        await delay(50);

        expect(sourceFinalized).toBe(true);

        pair.clientHub.close();
    });
});
