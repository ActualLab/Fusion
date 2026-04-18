 
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcClientPeer,
    RpcConnectionState,
    RpcType,
    defineRpcService,
} from '../src/index.js';
import { createTestHubPair, FORMATS, delay } from './rpc-test-helpers.js';
import type { TestHubPair } from './rpc-test-helpers.js';

interface ICalcService {
    add(a: number, b: number): Promise<number>;
    greet(name: string): Promise<string>;
    fail(): Promise<never>;
}

const CalcServiceDef = defineRpcService('CalcService', {
    add: { args: [0, 0] },
    greet: { args: [''] },
    fail: { args: [] },
});

describe.each(FORMATS)('RPC End-to-End [%s]', (formatKey) => {
    let pair: TestHubPair;

    beforeEach(async () => {
        pair = createTestHubPair(formatKey);

        // Register service on server
        pair.serverHub.addService(CalcServiceDef, {
            add: (a: unknown, b: unknown) => (a as number) + (b as number),
            greet: (name: unknown) => `Hello, ${String(name)}!`,
            fail: () => {
                throw new Error('intentional failure');
            },
        });

        await delay(10);
    });

    afterEach(() => {
        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('should call a remote method and get a result', async () => {
        const calc = pair.clientHub.addClient<ICalcService>(
            pair.clientPeer,
            CalcServiceDef
        );

        const result = await calc.add(3, 4);
        expect(result).toBe(7);
    });

    it('should call multiple methods', async () => {
        const calc = pair.clientHub.addClient<ICalcService>(
            pair.clientPeer,
            CalcServiceDef
        );

        const sum = await calc.add(10, 20);
        expect(sum).toBe(30);

        const greeting = await calc.greet('World');
        expect(greeting).toBe('Hello, World!');
    });

    it('should propagate server errors', async () => {
        const calc = pair.clientHub.addClient<ICalcService>(
            pair.clientPeer,
            CalcServiceDef
        );

        await expect(calc.fail()).rejects.toThrow('intentional failure');
    });

    it('should handle concurrent calls', async () => {
        const calc = pair.clientHub.addClient<ICalcService>(
            pair.clientPeer,
            CalcServiceDef
        );

        const [r1, r2, r3] = await Promise.all([
            calc.add(1, 2),
            calc.add(10, 20),
            calc.greet('Fusion'),
        ]);

        expect(r1).toBe(3);
        expect(r2).toBe(30);
        expect(r3).toBe('Hello, Fusion!');
    });

    it('should detect disconnection', async () => {
        expect(pair.clientPeer.isConnected).toBe(true);

        let clientDisconnected = false;
        pair.clientPeer.connectionStateChanged.add(state => {
            if (state === RpcConnectionState.Disconnected)
                clientDisconnected = true;
        });

        pair.clientPeer.close();
        await delay(10);
        expect(clientDisconnected).toBe(true);
    });

    it('should never throw from RpcConnection.send() on closed connection', async () => {
        const conn = pair.clientPeer.connection!;
        conn.close();
        await delay(10);

        // Should not throw
        expect(() => conn.send('test')).not.toThrow();
    });

    it('should handle noWait calls without registering in tracker', async () => {
        const noWaitDef = defineRpcService('NoWaitService', {
            fire: { args: [''], returns: RpcType.noWait },
        });

        let received: string | undefined;
        pair.serverHub.addService(noWaitDef, {
            fire: (msg: unknown) => {
                received = msg as string;
            },
        });

        // callNoWait should not throw and not register
        const trackerSizeBefore = pair.clientPeer.outboundCalls.size;
        pair.clientPeer.callNoWait('NoWaitService.fire:2', ['hello']);
        expect(pair.clientPeer.outboundCalls.size).toBe(trackerSizeBefore);

        await delay(50);
        expect(received).toBe('hello');
    });

    it('should work with addService/addClient unified API', async () => {
        const calc = pair.clientHub.addClient<ICalcService>(
            pair.clientPeer,
            CalcServiceDef
        );

        const result = await calc.add(5, 7);
        expect(result).toBe(12);

        const greeting = await calc.greet('addClient');
        expect(greeting).toBe('Hello, addClient!');
    });

    it('should resolve overloaded methods by argument count', async () => {
        const OverloadDef = defineRpcService('OverloadService', {
            compute: { args: [0] },
            'compute:2': { args: [0, 0] },
            'compute:3': { args: [0, 0, 0] },
        });

        pair.serverHub.addService(OverloadDef, {
            compute: (...args: unknown[]) => {
                const nums = args.map(Number);
                if (nums.length === 1) return nums[0] * 2;
                return nums.reduce((a, b) => a + b, 0);
            },
        });

        const svc = pair.clientHub.addClient<{
            compute(a: number, b?: number, c?: number): Promise<number>;
                }>(pair.clientPeer, OverloadDef);

        expect(await svc.compute(5)).toBe(10); // 1 arg → ×2
        expect(await svc.compute(3, 4)).toBe(7); // 2 args → sum
        expect(await svc.compute(1, 2, 3)).toBe(6); // 3 args → sum
    });

    it('should handle noWait call on disconnected peer silently', () => {
        const disconnectedPeer = new RpcClientPeer(pair.clientHub, 'ws://test-disconnected');
        // Not connected — should not throw
        expect(() =>
            disconnectedPeer.callNoWait('CalcService.add:3', [1, 2])
        ).not.toThrow();
    });
});
