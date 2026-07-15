import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { AsyncContext, errorResult } from '@actuallab/core';
import {
    Computed,
    ComputedOptions,
    ComputeFunction,
    ConsistencyState,
    StateBoundComputed,
    MutableState,
    computeMethod,
    wrapComputeMethod,
} from '../src/index.js';

let _testKeyCounter = 0;
function makeKey(method: string): string {
    return `Test.${method}[${++_testKeyCounter}]`;
}

function abortError(message = 'aborted'): Error {
    const e = new Error(message);
    e.name = 'AbortError';
    return e;
}

describe('K5 — error auto-invalidation via ComputedOptions', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });
    afterEach(() => {
        vi.useRealTimers();
    });

    it('per-kind defaults mirror C#: compute methods finite, state-bound Infinite', () => {
        expect(ComputedOptions.default.errorAutoInvalidateDelay).toBe(1000);
        expect(ComputedOptions.mutableStateDefault.errorAutoInvalidateDelay).toBe(Infinity);
        expect(new Computed<number>(makeKey('c')).options).toBe(ComputedOptions.default);
        const sbc = new StateBoundComputed<number>(new MutableState(0));
        expect(sbc.options).toBe(ComputedOptions.mutableStateDefault);
    });

    it('a MutableState holding an error never schedules auto-invalidation', () => {
        vi.useFakeTimers();
        const state = new MutableState<number>(0);
        state.set(errorResult<number>(new Error('fail')));
        expect(state.hasError).toBe(true);
        expect(vi.getTimerCount()).toBe(0);
        vi.advanceTimersByTime(60_000);
        expect(state.computed.isConsistent).toBe(true);
        expect(state.hasError).toBe(true);
    });

    it('a compute-method error auto-invalidates after the default delay', () => {
        vi.useFakeTimers();
        const c = new Computed<number>(makeKey('m'));
        c.setOutput(errorResult<number>(new Error('boom')));
        expect(c.isConsistent).toBe(true);
        vi.advanceTimersByTime(999);
        expect(c.isConsistent).toBe(true);
        vi.advanceTimersByTime(1);
        expect(c.state).toBe(ConsistencyState.Invalidated);
    });

    it('a per-declaration errorAutoInvalidateDelay override drives the timer', () => {
        vi.useFakeTimers();
        const c = new Computed<number>(
            makeKey('m'),
            undefined,
            new ComputedOptions({ errorAutoInvalidateDelay: 20 })
        );
        c.setOutput(errorResult<number>(new Error('boom')));
        vi.advanceTimersByTime(19);
        expect(c.isConsistent).toBe(true);
        vi.advanceTimersByTime(1);
        expect(c.state).toBe(ConsistencyState.Invalidated);
    });

    it('a per-declaration override reaches the compute method Computed', async () => {
        class Svc {
            @computeMethod({ errorAutoInvalidateDelay: 20 })
            val(): Promise<number> {
                return Promise.resolve(1);
            }
        }
        const svc = new Svc();
        const c = await Computed.capture(() => svc.val());
        expect(c.options.errorAutoInvalidateDelay).toBe(20);
    });

    it('the error timer is cancelled when the Computed is invalidated by other means', () => {
        vi.useFakeTimers();
        const c = new Computed<number>(makeKey('m'));
        c.setOutput(errorResult<number>(new Error('boom')));
        expect(vi.getTimerCount()).toBe(1);
        c.invalidate();
        expect(c.state).toBe(ConsistencyState.Invalidated);
        expect(vi.getTimerCount()).toBe(0);
    });
});

describe('K6 — cancellation errors are never served from cache', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('an AbortError-shaped throw is cached-but-immediately-invalidated; next caller recomputes', async () => {
        let calls = 0;
        const fn = wrapComputeMethod(() => {
            calls++;
            if (calls === 1) throw abortError();
            return 42;
        });
        await expect(fn()).rejects.toMatchObject({ name: 'AbortError' });
        const value = await fn();
        expect(value).toBe(42);
        expect(calls).toBe(2);
    });

    it('a non-cancellation error is cached (served without recompute within the error window)', async () => {
        let calls = 0;
        const fn = wrapComputeMethod(() => {
            calls++;
            throw new Error('boom');
        });
        await expect(fn()).rejects.toThrow('boom');
        await expect(fn()).rejects.toThrow('boom');
        expect(calls).toBe(1);
    });
});

describe('K13 — default arg keying fallback is empty string', () => {
    it("non-representable args map to '' and collide by design", () => {
        const cf = new ComputeFunction('m', () => 0);
        const instance = {};
        const keyUndefined = cf.buildKey(instance, [undefined]);
        const keyFunction = cf.buildKey(instance, [() => 1]);
        const keySymbol = cf.buildKey(instance, [Symbol('x')]);
        expect(keyUndefined.endsWith('\x1E')).toBe(true);
        expect(keyFunction).toBe(keyUndefined);
        expect(keySymbol).toBe(keyUndefined);
    });
});

describe('K15 — renewer does not retain the previous generation', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('gen-2 renewal uses a fresh renewer and re-runs with the original args', async () => {
        let runs = 0;
        const fn = wrapComputeMethod((x: number) => {
            runs++;
            return x * 10;
        });
        const c1 = await Computed.capture(() => fn(5));
        expect(c1.value).toBe(50);

        c1.invalidate();
        const c2 = await c1.update();
        expect(c2).not.toBe(c1);
        expect(c2.value).toBe(50);
        expect(runs).toBe(2);
        expect(c1._renewer).not.toBe(c2._renewer);
    });
});
