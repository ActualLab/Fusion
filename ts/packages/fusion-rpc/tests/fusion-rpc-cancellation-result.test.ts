import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { AsyncContext, isCancellation } from '@actuallab/core';
import {
    RpcClientPeer,
    RpcServerPeer,
    createMessageChannelPair,
} from '@actuallab/rpc';
import {
    FusionHub,
    RpcOutboundComputeCall,
    defineComputeService,
} from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

const FailingServiceDef = defineComputeService('FailingService', {
    fail: { args: [''] },
});

const REROUTE_TYPE_REF = 'ActualLab.Rpc.RpcRerouteException, ActualLab.Rpc';

describe('A cancellation result of a compute call', () => {
    let serverHub: FusionHub;
    let clientHub: FusionHub;

    beforeEach(() => {
        AsyncContext.current = undefined;
        serverHub = new FusionHub('server');
        clientHub = new FusionHub('client');
        serverHub.addService(FailingServiceDef, {
            fail(): number { throw new Error('boom'); },
        });
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    function connect(): { clientPeer: RpcClientPeer; serverPeer: RpcServerPeer } {
        const [clientConn, serverConn] = createMessageChannelPair();
        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientPeer.connectWith(clientConn);
        clientHub.addPeer(clientPeer);
        const serverPeer = serverHub.acceptRpcConnection(serverConn);
        return { clientPeer, serverPeer };
    }

    it('is invalidated at once on the client, which does not wait for $sys-c.Invalidate', async () => {
        const { clientPeer } = connect();
        // The server answers with what a .NET host sends when it can't run the call: RpcRerouteException
        serverHub.systemCallSender.errorFilter = (_error, info) => ({
            TypeRef: REROUTE_TYPE_REF,
            Message: info.Message,
        });
        await delay(10);

        const call = clientPeer.call('FailingService.fail:2', ['x'], {
            callTypeId: 1,
            outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m),
        }) as RpcOutboundComputeCall;
        const error: unknown = await call.result.promise.then(() => undefined, (e: unknown) => e);

        expect(isCancellation(error)).toBe(true);
        expect(call.whenInvalidated.isCompleted).toBe(true);
        expect(clientPeer.outboundCalls.get(call.callId)).toBeUndefined();
    });

    it('keeps an ordinary error registered until the server invalidates it', async () => {
        const { clientPeer } = connect();
        await delay(10);

        const call = clientPeer.call('FailingService.fail:2', ['x'], {
            callTypeId: 1,
            outboundCallFactory: (id, m) => new RpcOutboundComputeCall(id, m),
        }) as RpcOutboundComputeCall;
        const error: unknown = await call.result.promise.then(() => undefined, (e: unknown) => e);

        expect(isCancellation(error)).toBe(false);
        expect(call.whenInvalidated.isCompleted).toBe(false);
        expect(clientPeer.outboundCalls.get(call.callId)).toBe(call);
    });
});
