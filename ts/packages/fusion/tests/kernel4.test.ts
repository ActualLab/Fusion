// Regression tests for docs/plans/ts-port-audit.md kernel batch 4: K3, K11, K14.
import { describe, it, expect, beforeEach } from 'vitest';
import { AsyncContext, PromiseSource } from '@actuallab/core';
import { wrapComputeMethod, Computed, MutableState } from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('kernel4: K3 dependency capture after the first await', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('captures post-await dependencies via AsyncLocalStorage (Node default)', async () => {
        expect(AsyncContext.isAsyncLocalStorageActive).toBe(true);
        const a = new MutableState(1);
        const b = new MutableState(2);
        const total = wrapComputeMethod(async function total() {
            const av = a.use(); // sync prefix
            await delay(1);
            const bv = b.use(); // after the first await — flows via ALS
            return av + bv;
        });

        expect(await total()).toBe(3);
        a.set(10);
        expect(await total()).toBe(12);
        b.set(20);
        expect(await total()).toBe(30);
    });

    it('captures post-await dependencies via explicit ctx threading (ALS disabled)', async () => {
        AsyncContext._setAsyncLocalStorageActive(false);
        try {
            const a = new MutableState(1);
            const b = new MutableState(2);
            const total = wrapComputeMethod(async function total(
                ctx?: AsyncContext
            ) {
                const av = a.use(ctx); // sync prefix, ctx threaded
                await delay(1);
                const bv = b.use(ctx); // after await — captured only via ctx
                return av + bv;
            });

            expect(await total()).toBe(3);
            a.set(10);
            expect(await total()).toBe(12);
            b.set(20);
            expect(await total()).toBe(30);
        } finally {
            AsyncContext._setAsyncLocalStorageActive(true);
        }
    });
});

describe('kernel4: K11 Computed.capture rework', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('returns the last captured computed (C# last-wins)', async () => {
        const first = wrapComputeMethod(function first() {
            return 1;
        });
        const second = wrapComputeMethod(function second() {
            return 2;
        });

        const captured = await Computed.capture(async () => {
            await first();
            await second();
        });
        expect(captured.value).toBe(2);
        const dependants = (
            captured as unknown as { _dependants: Map<number, unknown> }
        )._dependants;
        expect(dependants.size).toBe(0);
    });

    it('nested captures record independently', async () => {
        const first = wrapComputeMethod(function firstNested() {
            return 1;
        });
        const second = wrapComputeMethod(function secondNested() {
            return 2;
        });

        let inner: Computed<number> | undefined;
        const outer = await Computed.capture(async () => {
            await first();
            inner = await Computed.capture(() => second());
        });
        expect(outer.value).toBe(1);
        expect(inner!.value).toBe(2);
    });

    it('returns the errored computed instead of rejecting', async () => {
        const failing = wrapComputeMethod(function failing() {
            throw new Error('boom');
        });

        const captured = await Computed.capture(() => failing());
        expect(captured.hasError).toBe(true);
        expect((captured.error as Error).message).toBe('boom');
    });

    it('captures a just-invalidated computed instead of throwing (no stub pollution)', async () => {
        const s = new MutableState(1);
        const gate = new PromiseSource<void>();
        const started = new PromiseSource<void>();
        const getVal = wrapComputeMethod(async function getVal() {
            const v = s.use(); // sync prefix — dependency captured
            started.resolve(undefined);
            await gate;
            return v;
        });

        const capturePromise = Computed.capture(() => getVal());
        await started;
        // Invalidate mid-flight: the in-flight computed self-invalidates on setOutput.
        s.set(2);
        gate.resolve(undefined);

        const captured = await capturePromise;
        expect(captured.value).toBe(1);
        expect(captured.isConsistent).toBe(false);
    });
});

describe('kernel4: K14 same-key reentrancy', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('fails fast on a same-key reentrant compute call instead of deadlocking', async () => {
        const recursive = wrapComputeMethod(async function recursiveImpl(): Promise<number> {
            return (await recursive()) + 1;
        });

        await expect(recursive()).rejects.toThrow('Reentrant compute call');
    }, 5000);

    it('fails fast on same-key recursion through Computed.capture', async () => {
        const recursive = wrapComputeMethod(async function recViaCapture(): Promise<number> {
            const c = await Computed.capture<number>(() => recursive());
            return c.value + 1;
        });

        await expect(recursive()).rejects.toThrow('Reentrant compute call');
    }, 5000);

    it('does not misfire on a context leaked past its computation', async () => {
        let leakedCtx: AsyncContext | undefined;
        const method = wrapComputeMethod(function leaky(ctx?: AsyncContext) {
            leakedCtx = ctx;
            return 1;
        });

        expect(await method()).toBe(1);
        method.invalidate();
        await expect(method(leakedCtx)).resolves.toBe(1);
    });

    it('allows a nested compute call with a different key', async () => {
        const leaf = wrapComputeMethod(function leaf(n: number) {
            return n * 2;
        });
        const root = wrapComputeMethod(async function root() {
            const x = await leaf(1);
            const y = await leaf(2);
            return x + y;
        });

        expect(await root()).toBe(6);
    }, 5000);
});
