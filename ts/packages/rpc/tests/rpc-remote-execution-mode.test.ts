import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcHub,
    RpcClientPeer,
    RpcType,
    RpcRemoteExecutionMode,
    RpcOutboundCall,
    defineRpcService,
    createRpcClient,
} from '../src';
import { RpcTestConnection } from './rpc-test-connection.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

// Service defs with various remoteExecutionMode settings
const NoneServiceDef = defineRpcService('NoneService', {
    add: { args: [0, 0], remoteExecutionMode: 0 },
});

const AwaitOnlyServiceDef = defineRpcService('AwaitService', {
    add: { args: [0, 0], remoteExecutionMode: RpcRemoteExecutionMode.AwaitForConnection },
});

const ReconnectServiceDef = defineRpcService('ReconnectService', {
    add: {
        args: [0, 0],
        remoteExecutionMode: RpcRemoteExecutionMode.AwaitForConnection | RpcRemoteExecutionMode.AllowReconnect,
    },
});

const DefaultServiceDef = defineRpcService('DefaultService', {
    add: { args: [0, 0] },
});

const NoWaitServiceDef = defineRpcService('NoWaitService', {
    fire: { args: [''], returns: RpcType.noWait },
});

interface ICalcService {
    add(a: number, b: number): Promise<number>;
}

const addImpl = (a: unknown, b: unknown) => (a as number) + (b as number);
const slowAddImpl = async (a: unknown, b: unknown) => {
    await delay(200);
    return (a as number) + (b as number);
};

describe('RpcRemoteExecutionMode', () => {
    let serverHub: RpcHub;
    let clientHub: RpcHub;
    let conn: RpcTestConnection;

    beforeEach(async () => {
        serverHub = new RpcHub('server-hub');
        clientHub = new RpcHub('client-hub');

        serverHub.addService(NoneServiceDef, { add: addImpl });
        serverHub.addService(AwaitOnlyServiceDef, { add: addImpl });
        serverHub.addService(ReconnectServiceDef, { add: addImpl });
        serverHub.addService(DefaultServiceDef, { add: addImpl });

        const clientPeer = new RpcClientPeer(clientHub, 'ws://test');
        clientHub.addPeer(clientPeer);

        conn = new RpcTestConnection(clientHub, serverHub, clientPeer);
        await conn.connect();
    });

    afterEach(() => {
        serverHub.close();
        clientHub.close();
    });

    // --- Method def tests ---

    it('remoteExecutionMode is set correctly on method def', () => {
        for (const [, def] of NoneServiceDef.methods) {
            expect(def.remoteExecutionMode).toBe(0);
        }
        for (const [, def] of AwaitOnlyServiceDef.methods) {
            expect(def.remoteExecutionMode).toBe(RpcRemoteExecutionMode.AwaitForConnection);
        }
        for (const [, def] of ReconnectServiceDef.methods) {
            expect(def.remoteExecutionMode).toBe(
                RpcRemoteExecutionMode.AwaitForConnection | RpcRemoteExecutionMode.AllowReconnect
            );
        }
        for (const [, def] of DefaultServiceDef.methods) {
            expect(def.remoteExecutionMode).toBe(RpcRemoteExecutionMode.Default);
        }
    });

    it('NoWait methods get remoteExecutionMode 0', () => {
        for (const [, def] of NoWaitServiceDef.methods) {
            expect(def.remoteExecutionMode).toBe(0);
        }
    });

    it('RpcOutboundCall stores remoteExecutionMode', () => {
        const call = new RpcOutboundCall(1, 'test', 3);
        expect(call.remoteExecutionMode).toBe(3);
    });

    it('RpcOutboundCall defaults to 7 (Default)', () => {
        const call = new RpcOutboundCall(1, 'test');
        expect(call.remoteExecutionMode).toBe(7);
    });

    // --- None mode tests ---

    it('None mode: works when connected', async () => {
        const calc = clientHub.addClient<ICalcService>(conn.clientPeer, NoneServiceDef);
        const result = await calc.add(1, 2);
        expect(result).toBe(3);
    });

    it('None mode: fails when disconnected', async () => {
        const calc = clientHub.addClient<ICalcService>(conn.clientPeer, NoneServiceDef);
        await conn.disconnect();

        await expect(calc.add(1, 2)).rejects.toThrow('AwaitForConnection');
    });

    it('None mode: in-flight call aborted on reconnect', async () => {
        // Use a slow server
        const slowHub = new RpcHub('slow-server');
        slowHub.addService(NoneServiceDef, { add: slowAddImpl });
        await conn.switchHost(slowHub);

        const calc = clientHub.addClient<ICalcService>(conn.clientPeer, NoneServiceDef);
        const promise = calc.add(10, 20);
        // Prevent unhandled rejection
        promise.catch(() => { /* prevent unhandled rejection */ });

        // Reconnect while call is in-flight
        await conn.reconnect();

        await expect(promise).rejects.toThrow('AllowReconnect');
        slowHub.close();
    });

    // --- AwaitForConnection only tests ---

    it('AwaitForConnection only: aborted on reconnect', async () => {
        const slowHub = new RpcHub('slow-server');
        slowHub.addService(AwaitOnlyServiceDef, { add: slowAddImpl });
        await conn.switchHost(slowHub);

        const calc = clientHub.addClient<ICalcService>(conn.clientPeer, AwaitOnlyServiceDef);
        const promise = calc.add(10, 20);
        promise.catch(() => { /* prevent unhandled rejection */ });

        await conn.reconnect();

        await expect(promise).rejects.toThrow('AllowReconnect');
        slowHub.close();
    });

    // --- AllowReconnect tests ---

    it('AllowReconnect (no AllowResend): aborted on peer change', async () => {
        // connectWith() always treats reconnection as peer-changed,
        // so AllowReconnect without AllowResend will be aborted
        const slowHub = new RpcHub('slow-server');
        slowHub.addService(ReconnectServiceDef, { add: slowAddImpl });
        await conn.switchHost(slowHub);

        const calc = clientHub.addClient<ICalcService>(conn.clientPeer, ReconnectServiceDef);
        const promise = calc.add(10, 20);
        promise.catch(() => { /* prevent unhandled rejection */ });

        await conn.reconnect();

        await expect(promise).rejects.toThrow('AllowResend');
        slowHub.close();
    });

    // --- Default mode tests ---

    it('Default: works when connected', async () => {
        const calc = clientHub.addClient<ICalcService>(conn.clientPeer, DefaultServiceDef);
        const result = await calc.add(5, 7);
        expect(result).toBe(12);
    });

    it('Default: survives reconnect', async () => {
        const slowHub = new RpcHub('slow-server');
        slowHub.addService(DefaultServiceDef, { add: slowAddImpl });
        await conn.switchHost(slowHub);

        const calc = clientHub.addClient<ICalcService>(conn.clientPeer, DefaultServiceDef);
        const promise = calc.add(3, 4);

        await conn.reconnect();

        const result = await promise;
        expect(result).toBe(7);
        slowHub.close();
    });

    // --- createRpcClient (legacy) tests ---

    it('createRpcClient: None mode fails when disconnected', async () => {
        const calc = createRpcClient<ICalcService>(conn.clientPeer, NoneServiceDef);
        await conn.disconnect();

        await expect(calc.add(1, 2)).rejects.toThrow('AwaitForConnection');
    });

    it('createRpcClient: Default mode works when connected', async () => {
        const calc = createRpcClient<ICalcService>(conn.clientPeer, DefaultServiceDef);
        const result = await calc.add(1, 2);
        expect(result).toBe(3);
    });
});
