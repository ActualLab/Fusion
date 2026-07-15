import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { RpcType, defineRpcService } from '../src/index.js';
import type { RpcStream } from '../src/index.js';
import { createTestHubPair, delay } from './rpc-test-helpers.js';
import type { TestHubPair } from './rpc-test-helpers.js';

interface IStateService {
    getValue(n: number): Promise<string>;
    addSecret(n: number): Promise<number>;
    fire(n: number): void;
    countUp(count: number): Promise<RpcStream<number>>;
}

class StateService {
    prefix = 'v=';
    lastFired = 0;
    #secret = 100;

    getValue(n: number): string {
        return this.prefix + String(n);
    }

    addSecret(n: number): number {
        return this.#secret + n;
    }

    fire(n: number): void {
        this.lastFired = this.#secret + n;
    }

    // eslint-disable-next-line @typescript-eslint/require-await -- async generator used as an RPC stream source
    async *countUp(count: number): AsyncGenerator<number> {
        for (let i = 0; i < count; i++)
            yield this.#secret + i;
    }
}

const StateServiceDef = defineRpcService('StateService', {
    getValue: { args: [0] },
    addSecret: { args: [0] },
    fire: { args: [0], returns: RpcType.noWait },
    countUp: { args: [0], returns: RpcType.stream },
});

describe('RpcServiceHost dispatch receiver (R18)', () => {
    let pair: TestHubPair;
    let impl: StateService;

    beforeEach(async () => {
        pair = createTestHubPair('json5np');
        impl = new StateService();
        pair.serverHub.addService(StateServiceDef, impl as unknown as Record<string, (...a: unknown[]) => unknown>);
        await delay(10);
    });

    afterEach(() => {
        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('regular method reads ordinary instance state', async () => {
        const svc = pair.clientHub.addClient<IStateService>(pair.clientPeer, StateServiceDef);
        expect(await svc.getValue(5)).toBe('v=5');
    });

    it('regular method reads #private instance state', async () => {
        const svc = pair.clientHub.addClient<IStateService>(pair.clientPeer, StateServiceDef);
        expect(await svc.addSecret(5)).toBe(105);
    });

    it('noWait method reads #private instance state', async () => {
        const svc = pair.clientHub.addClient<IStateService>(pair.clientPeer, StateServiceDef);
        svc.fire(7);
        await delay(50);
        expect(impl.lastFired).toBe(107);
    });

    it('stream method reads #private instance state', async () => {
        const svc = pair.clientHub.addClient<IStateService>(pair.clientPeer, StateServiceDef);
        const stream = await svc.countUp(3);
        const items: number[] = [];
        for await (const x of stream)
            items.push(x);

        expect(items).toEqual([100, 101, 102]);
    });
});
