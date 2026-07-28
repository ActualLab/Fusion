// Regression tests for the compute-call ↔ Computed ownership rules (review findings
// I1 and I10). The server owns its inbound computed strongly until it has sent the
// invalidation; the client's outbound call must never own its computed at all.
// Both assert what GC reclaims, so they need `--expose-gc` (see vitest.config.ts).
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { AsyncContext } from '@actuallab/core';
import { Computed, MutableState } from '@actuallab/fusion';
import {
    RpcClientPeer,
    RpcType,
    createMessageChannelPair,
    defineRpcService,
} from '@actuallab/rpc';
import {
    FusionHub,
    RpcOutboundComputeCall,
    defineComputeService,
} from '../src/index.js';

const gc = (globalThis as { gc?: () => void }).gc;

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

async function collect(passes = 3): Promise<void> {
    for (let i = 0; i < passes; i++) {
        gc!();
        await delay(20);
    }
}

interface ComputeService {
    getValue(key: string): Promise<number>;
}

const ComputeDef = defineComputeService('Svc', { getValue: { args: [''] } });
const MutationDef = defineRpcService('Mut', {
    set: { args: ['', 0], returns: RpcType.noWait },
});

describe.skipIf(gc === undefined)('Compute call ↔ Computed lifetime (I1, I10)', () => {
    let serverHub: FusionHub;
    let clientHub: FusionHub;
    let clientPeer: RpcClientPeer;
    const store = new Map<string, MutableState<number>>();

    function getState(key: string): MutableState<number> {
        let state = store.get(key);
        if (state === undefined) {
            state = new MutableState(0);
            store.set(key, state);
        }
        return state;
    }

    beforeEach(async () => {
        AsyncContext.current = undefined;
        store.clear();
        serverHub = new FusionHub('server');
        clientHub = new FusionHub('client');
        serverHub.addService(ComputeDef, {
            getValue: key => getState(key as string).use(),
        });
        serverHub.addService(MutationDef, {
            set: (key, value) => getState(key as string).set(value as number),
        });

        const [clientConn, serverConn] = createMessageChannelPair();
        clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        serverHub.acceptRpcConnection(serverConn);
        await delay(20);
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    it('I1: a server-side computed still invalidates after a GC', async () => {
        const call = clientPeer.call('Svc.getValue:2', ['x'], {
            callTypeId: 1,
            outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m),
        }) as RpcOutboundComputeCall;
        expect(await call.result.promise).toBe(0);
        await collect();

        clientPeer.callNoWait('Mut.set:3', ['x', 42]);
        await delay(100);

        expect(call.whenInvalidated.isCompleted).toBe(true);
    });

    it('I10: a collected client computed unregisters its outbound call', async () => {
        const proxy = clientHub.addClient<ComputeService>(clientPeer, ComputeDef);

        let collectedCount = 0;
        const watcher = new FinalizationRegistry(() => {
            collectedCount++;
        });
        for (let i = 0; i < 50; i++) {
            const computed = await Computed.capture(() => proxy.getValue(`k${i}`));
            watcher.register(computed, i);
        }
        expect(clientPeer.outboundCalls.size).toBe(50);

        await collect();

        expect(collectedCount).toBeGreaterThan(40);
        expect(clientPeer.outboundCalls.size).toBeLessThan(10);
    });

    it('I10: a live client computed keeps its outbound call registered', async () => {
        const proxy = clientHub.addClient<ComputeService>(clientPeer, ComputeDef);
        const computed = await Computed.capture(() => proxy.getValue('kept'));

        await collect();

        expect(clientPeer.outboundCalls.size).toBe(1);
        expect(computed.isConsistent).toBe(true);

        getState('kept').set(7);
        await delay(100);

        expect(computed.isConsistent).toBe(false);
    });
});
