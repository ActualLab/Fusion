 
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    AsyncContext,
    PromiseSource,
    abortSignalKey,
    isCancellation,
} from '@actuallab/core';
import { Computed, MutableState, computeMethod } from '@actuallab/fusion';
import {
    RpcClientPeer,
    RpcType,
    RpcRemoteExecutionMode,
    RpcSerializationFormat,
    RpcWebSocketConnection,
    rpcService,
    rpcMethod,
    defineRpcService,
    createRpcClient,
    createMessageChannelPair,
} from '@actuallab/rpc';
import {
    FusionHub,
    RpcOutboundComputeCall,
    defineComputeService,
} from '../src/index.js';
import { createMockWsPair } from '../../rpc/tests/mock-ws.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

// Decorator-based contract class
@rpcService('CounterService')
class ICounterService {
    @computeMethod
    @rpcMethod()
    getCount(_key: string): number {
        return undefined!;
    }

    @computeMethod
    @rpcMethod()
    getDoubled(_key: string): number {
        return undefined!;
    }
}

// Legacy service def for mutation (non-compute)
const MutationServiceDef = defineRpcService('MutationService', {
    setCount: { args: ['', 0], returns: RpcType.noWait },
});

// Compute service whose method can be gated mid-computation (F1).
@rpcService('SlowCounterService')
class ISlowCounterService {
    @computeMethod
    @rpcMethod()
    getCount(_key: string): number {
        return undefined!;
    }
}

// Mixed-mode service to check that FusionHub preserves noWait /
// remoteExecutionMode metadata for non-compute methods (F10).
@rpcService('ModeService')
class IModeService {
    @computeMethod
    @rpcMethod()
    getValue(_key: string): number {
        return undefined!;
    }

    @rpcMethod({ returns: RpcType.noWait })
    // eslint-disable-next-line @typescript-eslint/no-empty-function
    poke(_key: string): void {}

    @rpcMethod({ remoteExecutionMode: RpcRemoteExecutionMode.AwaitForConnection })
    special(_key: string): number {
        return undefined!;
    }
}

describe('End-to-end Fusion over RPC', () => {
    let serverHub: FusionHub;
    let clientHub: FusionHub;
    const store = new Map<string, MutableState<number>>();

    function getState(key: string): MutableState<number> {
        let s = store.get(key);
        if (s === undefined) {
            s = new MutableState(0);
            store.set(key, s);
        }
        return s;
    }

    beforeEach(() => {
        AsyncContext.current = undefined;
        store.clear();

        serverHub = new FusionHub('server');
        clientHub = new FusionHub('client');

        // Register compute service on server using contract class
        serverHub.addService(ICounterService, {
            getCount(key: unknown): number {
                return getState(key as string).use();
            },
            getDoubled(key: unknown): number {
                return getState(key as string).use() * 2;
            },
        });

        // Register mutation service (non-compute, noWait)
        serverHub.addService(MutationServiceDef, {
            setCount(key: unknown, value: unknown) {
                getState(key as string).set(value as number);
            },
        });
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    it('should call a compute method and get result', async () => {
        const [clientConn, serverConn] = createMessageChannelPair();

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);

        serverHub.acceptRpcConnection(serverConn);

        await delay(10);

        // Use legacy service def for client (decorator-based client coming in future)
        const counterDef = defineComputeService('CounterService', {
            getCount: { args: [''] },
            getDoubled: { args: [''] },
        });

        const counter = createRpcClient<{
            getCount(key: string): Promise<number>;
            getDoubled(key: string): Promise<number>;
                }>(clientPeer, counterDef);
        getState('x').set(42);

        const result = await counter.getCount('x');
        expect(result).toBe(42);
    });

    it('should receive $sys-c.Invalidate when server-side computed is invalidated', async () => {
        const [clientConn, serverConn] = createMessageChannelPair();

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);

        serverHub.acceptRpcConnection(serverConn);

        await delay(10);

        // Make a compute call
        const outboundCall = clientPeer.call(
            'CounterService.getCount:2',
            ['x'],
            {
                callTypeId: 1,
                outboundCallFactory: (id, m) =>
                    new RpcOutboundComputeCall(id, m),
            }
        ) as RpcOutboundComputeCall;

        const result = await outboundCall.result.promise;
        expect(result).toBe(0); // default value

        // Trigger server-side invalidation via mutation (noWait)
        clientPeer.callNoWait('MutationService.setCount:3', ['x', 100]);

        // Wait for invalidation notification
        await delay(50);
        expect(outboundCall.whenInvalidated.isCompleted).toBe(true);
    });

    it('should call multiple compute methods', async () => {
        const [clientConn, serverConn] = createMessageChannelPair();

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);

        serverHub.acceptRpcConnection(serverConn);

        await delay(10);

        const counterDef = defineComputeService('CounterService', {
            getCount: { args: [''] },
            getDoubled: { args: [''] },
        });

        const counter = createRpcClient<{
            getCount(key: string): Promise<number>;
            getDoubled(key: string): Promise<number>;
                }>(clientPeer, counterDef);
        getState('a').set(10);

        const count = await counter.getCount('a');
        expect(count).toBe(10);

        const doubled = await counter.getDoubled('a');
        expect(doubled).toBe(20);
    });

    it('should capture RPC compute call via Computed.capture()', async () => {
        const [clientConn, serverConn] = createMessageChannelPair();

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);

        serverHub.acceptRpcConnection(serverConn);

        await delay(10);

        const counterDef = defineComputeService('CounterService', {
            getCount: { args: [''] },
        });
        const counter = clientHub.addClient<{
            getCount(key: string): Promise<number>;
                }>(clientPeer, counterDef);
        getState('x').set(42);

        const captured = await Computed.capture(() => counter.getCount('x'));
        expect(captured.value).toBe(42);
        expect(captured.isConsistent).toBe(true);
    });

    it('should observe server-side invalidation via Computed.capture()', async () => {
        const [clientConn, serverConn] = createMessageChannelPair();

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);

        serverHub.acceptRpcConnection(serverConn);

        await delay(10);

        const counterDef = defineComputeService('CounterService', {
            getCount: { args: [''] },
        });
        const counter = clientHub.addClient<{
            getCount(key: string): Promise<number>;
                }>(clientPeer, counterDef);

        const captured = await Computed.capture(() => counter.getCount('x'));
        expect(captured.value).toBe(0);

        // Trigger server-side invalidation via mutation
        clientPeer.callNoWait('MutationService.setCount:3', ['x', 100]);

        // Wait for invalidation notification
        await captured.whenInvalidated();
        expect(captured.isConsistent).toBe(false);
    });

    it('F1: $sys-c.Invalidate is sent even when the computed is invalidated mid-computation', async () => {
        const gate = new PromiseSource<void>();
        const started = new PromiseSource<void>();

        serverHub.addService(ISlowCounterService, {
            async getCount(key: unknown): Promise<number> {
                const v = getState(key as string).use(); // sync prefix — dep captured
                started.resolve(undefined);
                await gate;
                return v;
            },
        });

        const [clientConn, serverConn] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(10);

        const outboundCall = clientPeer.call(
            'SlowCounterService.getCount:2',
            ['x'],
            {
                callTypeId: 1,
                outboundCallFactory: (id, m) =>
                    new RpcOutboundComputeCall(id, m),
            }
        ) as RpcOutboundComputeCall;

        // Invalidate the server computed while it is still Computing.
        await started;
        clientPeer.callNoWait('MutationService.setCount:3', ['x', 100]);
        await delay(20);
        gate.resolve(undefined);

        const result = await outboundCall.result.promise;
        expect(result).toBe(0); // stale-marked value — matches C#

        // C# ProcessStage2 awaits WhenInvalidated (immediate for an
        // already-invalidated computed) and still notifies the client.
        await delay(50);
        expect(outboundCall.whenInvalidated.isCompleted).toBe(true);
    });

    it('F7: a regular (non-compute) call to a compute method skips invalidation tracking', async () => {
        const [clientConn, serverConn] = createMessageChannelPair();

        const serverFrames: string[] = [];
        const origSend = serverConn.send.bind(serverConn);
        serverConn.send = (m: string) => {
            serverFrames.push(m);
            origSend(m);
        };

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(10);

        // Regular (CallType 0) call to a compute method.
        const regular = clientPeer.call('CounterService.getCount:2', ['r']);
        expect(await regular.result.promise).toBe(0);

        clientPeer.callNoWait('MutationService.setCount:3', ['r', 5]);
        await delay(30);
        expect(serverFrames.some(f => f.includes('$sys-c.Invalidate'))).toBe(
            false
        );

        // Positive control: a compute (CallType 1) call to the same method is tracked.
        const compute = clientPeer.call(
            'CounterService.getCount:2',
            ['c'],
            {
                callTypeId: 1,
                outboundCallFactory: (id, m) =>
                    new RpcOutboundComputeCall(id, m),
            }
        ) as RpcOutboundComputeCall;
        expect(await compute.result.promise).toBe(0);

        clientPeer.callNoWait('MutationService.setCount:3', ['c', 9]);
        await delay(50);
        expect(compute.whenInvalidated.isCompleted).toBe(true);
        expect(serverFrames.some(f => f.includes('$sys-c.Invalidate'))).toBe(
            true
        );
    });

    it('F9: invalidation is sent via the peer serialization format (msgpack, no JSON frame)', async () => {
        const format = RpcSerializationFormat.get('msgpack6');
        const [clientWs, serverWs] = createMockWsPair();

        // The invalidation must go out as a binary msgpack frame, never a
        // hand-rolled JSON text frame (a .NET client would reject the latter).
        const serverTextFrames: string[] = [];
        const origServerSend = serverWs.send.bind(serverWs);
        serverWs.send = (
            data: string | ArrayBufferLike | Uint8Array | ArrayBufferView
        ) => {
            if (typeof data === 'string') serverTextFrames.push(data);
            origServerSend(data);
        };

        const clientConn = new RpcWebSocketConnection(
            clientWs,
            true,
            format,
            clientHub.registry
        );
        const serverConn = new RpcWebSocketConnection(
            serverWs,
            true,
            format,
            serverHub.registry
        );

        const clientPeer = new RpcClientPeer(
            clientHub,
            'ws://test?f=msgpack6',
            false
        );
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);

        const serverPeer = serverHub.getServerPeer(
            `server://${crypto.randomUUID()}`
        );
        serverPeer.serializationFormat = format;
        serverPeer.accept(serverConn);
        await delay(20);

        const outboundCall = clientPeer.call(
            'CounterService.getCount:2',
            ['m'],
            {
                callTypeId: 1,
                outboundCallFactory: (id, m) =>
                    new RpcOutboundComputeCall(id, m),
            }
        ) as RpcOutboundComputeCall;
        expect(await outboundCall.result.promise).toBe(0);

        clientPeer.callNoWait('MutationService.setCount:3', ['m', 7]);
        await delay(50);

        expect(outboundCall.whenInvalidated.isCompleted).toBe(true);
        expect(
            serverTextFrames.some(f => f.includes('$sys-c.Invalidate'))
        ).toBe(false);
    });

    it('F10: decorator noWait / remoteExecutionMode survive registration through FusionHub', () => {
        serverHub.addService(IModeService, {
            getValue(key: unknown): number {
                return getState(key as string).use();
            },
            // eslint-disable-next-line @typescript-eslint/no-empty-function
            poke() {},
            special(): number {
                return 0;
            },
        });

        const getValueDef =
            serverHub.serviceHost.getMethodDef('ModeService.getValue:2');
        expect(getValueDef?.callTypeId).toBe(1); // compute → FUSION_CALL_TYPE_ID
        expect(getValueDef?.noWait).toBe(false);

        const pokeDef = serverHub.serviceHost.getMethodDef('ModeService.poke:2');
        expect(pokeDef?.noWait).toBe(true);
        expect(pokeDef?.remoteExecutionMode).toBe(0);
        expect(pokeDef?.callTypeId).toBe(0);

        const specialDef =
            serverHub.serviceHost.getMethodDef('ModeService.special:2');
        expect(specialDef?.remoteExecutionMode).toBe(
            RpcRemoteExecutionMode.AwaitForConnection
        );
        expect(specialDef?.callTypeId).toBe(0);
        expect(specialDef?.noWait).toBe(false);
    });

    it('F2: $sys-c.Invalidate arriving before the result settles the call (cancellation-shaped), never hangs', async () => {
        const gate = new PromiseSource<void>();
        const started = new PromiseSource<void>();
        serverHub.addService(ISlowCounterService, {
            async getCount(_key: unknown): Promise<number> {
                started.resolve(undefined);
                await gate;
                return 1;
            },
        });

        const [clientConn, serverConn] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(10);

        let callId = -1;
        const outboundCall = clientPeer.call(
            'SlowCounterService.getCount:2',
            ['x'],
            {
                callTypeId: 1,
                outboundCallFactory: (id, m) => {
                    callId = id;
                    return new RpcOutboundComputeCall(id, m);
                },
            }
        ) as RpcOutboundComputeCall;

        await started;
        clientHub.systemCallHandler.handle(
            { Method: '$sys-c.Invalidate:0', RelatedId: callId },
            [],
            clientPeer
        );

        let rejection: unknown;
        const outcome = await Promise.race([
            outboundCall.result.promise.then(
                () => 'resolved',
                (e: unknown) => {
                    rejection = e;
                    return 'rejected';
                }
            ),
            delay(500).then(() => 'timeout'),
        ]);
        expect(outcome).toBe('rejected');
        expect(isCancellation(rejection)).toBe(true);
        expect(outboundCall.whenInvalidated.isCompleted).toBe(true);

        gate.resolve(undefined);
    });

    it('F2: a non-compute call with the same id survives a stray $sys-c.Invalidate', async () => {
        const gate = new PromiseSource<void>();
        const started = new PromiseSource<void>();
        serverHub.addService(ISlowCounterService, {
            async getCount(_key: unknown): Promise<number> {
                started.resolve(undefined);
                await gate;
                return 7;
            },
        });

        const [clientConn, serverConn] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(10);

        // A regular (non-compute) outbound call.
        const call = clientPeer.call('SlowCounterService.getCount:2', ['x']);
        const id = call.callId;

        await started;
        clientHub.systemCallHandler.handle(
            { Method: '$sys-c.Invalidate:0', RelatedId: id },
            [],
            clientPeer
        );

        // The stray Invalidate must not evict a non-compute call.
        expect(clientPeer.outboundCalls.get(id)).toBe(call);
        expect(call.result.isCompleted).toBe(false);

        gate.resolve(undefined);
        expect(await call.result.promise).toBe(7);
    });

    it('F3: a cancelled call is never served from the compute cache to the next caller', async () => {
        const gate = new PromiseSource<void>();
        serverHub.addService(ISlowCounterService, {
            async getCount(_key: unknown): Promise<number> {
                await gate;
                return 1;
            },
        });

        const [clientConn, serverConn] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(10);

        const counterDef = defineComputeService('SlowCounterService', {
            getCount: { args: [''] },
        });
        const counter = clientHub.addClient<{
            getCount(key: string, ctx?: AsyncContext): Promise<number>;
                }>(clientPeer, counterDef);

        const ac = new AbortController();
        const ctx = new AsyncContext().with(abortSignalKey, ac.signal);
        const outcomeA = counter
            .getCount('x', ctx)
            .then(() => 'resolved', (e: unknown) => (isCancellation(e) ? 'cancelled' : 'error'));
        await delay(20);
        ac.abort();
        expect(await outcomeA).toBe('cancelled');

        // The next caller must recompute a fresh value, not receive A's cancellation.
        gate.resolve(undefined);
        const valueB = await counter.getCount('x');
        expect(valueB).toBe(1);
    });

    it('F3: peer-closed rejections are cancellation-shaped (never cached by K6)', async () => {
        const gate = new PromiseSource<void>();
        const started = new PromiseSource<void>();
        serverHub.addService(ISlowCounterService, {
            async getCount(_key: unknown): Promise<number> {
                started.resolve(undefined);
                await gate;
                return 1;
            },
        });

        const [clientConn, serverConn] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(10);

        const call = clientPeer.call('SlowCounterService.getCount:2', ['x'], {
            callTypeId: 1,
            outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m),
        }) as RpcOutboundComputeCall;
        const caught = call.result.promise.then(
            () => 'resolved',
            (e: unknown) => (isCancellation(e) ? 'cancelled' : 'error')
        );

        await started;
        clientPeer.close();
        expect(await caught).toBe('cancelled');

        gate.resolve(undefined);
    });

    it('F3: a genuine remote error computed still tracks server invalidation', async () => {
        serverHub.addService(ISlowCounterService, {
            getCount(key: unknown): number {
                const value = getState(key as string).use();
                if (value === 0)
                    throw new Error('Not ready.');

                return value;
            },
        });

        const [clientConn, serverConn] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(10);

        const counterDef = defineComputeService('SlowCounterService', {
            getCount: { args: [''] },
        });
        const counter = clientHub.addClient<{
            getCount(key: string): Promise<number>;
                }>(clientPeer, counterDef);

        await expect(counter.getCount('x')).rejects.toThrow('Not ready.');

        // C# SetError parity: the errored compute call must stay tracked.
        const computeCalls = [
            ...clientPeer.outboundCalls.values(),
        ].filter(c => c instanceof RpcOutboundComputeCall);
        expect(computeCalls.length).toBe(1);

        // Server-side invalidation must reach the error computed well before
        // its 1 s error-cache expiry, so the next call recomputes.
        getState('x').set(5);
        await delay(50);
        expect(await counter.getCount('x')).toBe(5);
    });

    it('F4: local invalidation of a bound client computed sends $sys.Cancel and unregisters the call', async () => {
        const [clientConn, serverConn] = createMessageChannelPair();
        const clientFrames: string[] = [];
        const origSend = clientConn.send.bind(clientConn);
        clientConn.send = (m: string) => {
            clientFrames.push(m);
            origSend(m);
        };

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(10);

        const counterDef = defineComputeService('CounterService', {
            getCount: { args: [''] },
        });
        const counter = clientHub.addClient<{
            getCount(key: string): Promise<number>;
                }>(clientPeer, counterDef);

        const captured = await Computed.capture(() => counter.getCount('x'));
        expect(captured.value).toBe(0);

        const computeCallsBefore = [
            ...clientPeer.outboundCalls.values(),
        ].filter(c => c instanceof RpcOutboundComputeCall);
        expect(computeCallsBefore.length).toBe(1);

        captured.invalidate();
        await delay(20);

        expect(clientFrames.some(f => f.includes('$sys.Cancel'))).toBe(true);
        const computeCallsAfter = [
            ...clientPeer.outboundCalls.values(),
        ].filter(c => c instanceof RpcOutboundComputeCall);
        expect(computeCallsAfter.length).toBe(0);
    });

    it('F5: an aborted compute client call rejects promptly and is not cached', async () => {
        const gate = new PromiseSource<void>();
        serverHub.addService(ISlowCounterService, {
            async getCount(_key: unknown): Promise<number> {
                await gate;
                return 1;
            },
        });

        const [clientConn, serverConn] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(10);

        const counterDef = defineComputeService('SlowCounterService', {
            getCount: { args: [''] },
        });
        const counter = clientHub.addClient<{
            getCount(key: string, ctx?: AsyncContext): Promise<number>;
                }>(clientPeer, counterDef);

        const ac = new AbortController();
        const ctx = new AsyncContext().with(abortSignalKey, ac.signal);
        const p = counter.getCount('x', ctx);
        await delay(20);
        ac.abort();

        let rejection: unknown;
        const outcome = await Promise.race([
            p.then(
                () => 'resolved',
                (e: unknown) => {
                    rejection = e;
                    return 'rejected';
                }
            ),
            delay(500).then(() => 'timeout'),
        ]);
        expect(outcome).toBe('rejected');
        expect(isCancellation(rejection)).toBe(true);

        // The abort must not be cached: a fresh call recomputes a real value.
        gate.resolve(undefined);
        expect(await counter.getCount('x')).toBe(1);
    });
});
