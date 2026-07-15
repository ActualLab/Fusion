import { describe, it, expect, beforeEach } from 'vitest';
import { AsyncContext } from '@actuallab/core';
import {
    Computed,
    ConsistencyState,
    ComputeContext,
    computeContextKey,
    computeMethod,
    ComputedState,
    FixedDelayer,
} from '../src/index.js';

let _testKeyCounter = 0;
function makeKey(method: string, ...args: unknown[]): string {
    return `Kernel2.${method}[${++_testKeyCounter}]:${args.map(a => JSON.stringify(a)).join(',')}`;
}

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('K4 — invalidate() never throws', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('a throwing onInvalidated handler does not abort the cascade', () => {
        const child = new Computed<number>(makeKey('child'));
        child.setOutput(1);
        const parentA = new Computed<number>(makeKey('parentA'));
        parentA.addDependency(child);
        parentA.setOutput(10);
        const parentB = new Computed<number>(makeKey('parentB'));
        parentB.addDependency(child);
        parentB.setOutput(20);

        child.onInvalidated(() => {
            throw new Error('boom');
        });

        expect(() => child.invalidate()).not.toThrow();
        expect(child.state).toBe(ConsistencyState.Invalidated);
        expect(parentA.state).toBe(ConsistencyState.Invalidated);
        expect(parentB.state).toBe(ConsistencyState.Invalidated);
    });

    it('ComputedState update loop survives a throwing onInvalidated handler', async () => {
        let counter = 0;
        const state = new ComputedState<number>(() => ++counter, {
            updateDelayer: FixedDelayer.zero,
        });
        await state.whenFirstTimeUpdated();
        expect(state.value).toBe(1);

        state.computed.onInvalidated(() => {
            throw new Error('boom');
        });
        state.computed.invalidate();
        await delay(20);
        expect(state.value).toBe(2);

        state.computed.invalidate();
        await delay(20);
        expect(state.value).toBe(3);

        state.dispose();
    });
});

describe('K17 — invalidation ordering', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('own onInvalidated handlers fire before dependant propagation', () => {
        const order: string[] = [];
        const child = new Computed<number>(makeKey('child'));
        child.setOutput(1);
        const parent = new Computed<number>(makeKey('parent'));
        parent.addDependency(child);
        parent.setOutput(10);

        parent.onInvalidated(() => order.push('parent'));
        child.onInvalidated(() => order.push('child'));

        child.invalidate();
        expect(order).toEqual(['child', 'parent']);
    });
});

describe('K12 — onInvalidated is a method with immediate-fire semantics', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('a subscription added after invalidation fires immediately', () => {
        const c = new Computed<number>(makeKey('get'));
        c.setOutput(42);
        c.invalidate();
        expect(c.state).toBe(ConsistencyState.Invalidated);

        let fired = false;
        c.onInvalidated(() => {
            fired = true;
        });
        expect(fired).toBe(true);
    });
});

describe('K10 — whenInvalidated cleanup and already-aborted', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('rejects immediately on an already-aborted signal', async () => {
        const c = new Computed<number>(makeKey('get'));
        c.setOutput(42);
        const ac = new AbortController();
        ac.abort();
        await expect(c.whenInvalidated(ac.signal)).rejects.toBeDefined();
    });

    it('invalidation removes the abort listener', async () => {
        const c = new Computed<number>(makeKey('get'));
        c.setOutput(42);
        const ac = new AbortController();
        let removed = 0;
        const origRemove = ac.signal.removeEventListener.bind(ac.signal);
        ac.signal.removeEventListener = ((
            type: string,
            listener: EventListenerOrEventListenerObject,
            opts?: boolean | EventListenerOptions
        ) => {
            if (type === 'abort') removed++;
            origRemove(type, listener, opts);
        }) as typeof ac.signal.removeEventListener;

        const p = c.whenInvalidated(ac.signal);
        c.invalidate();
        await p;
        expect(removed).toBe(1);
    });

    it('abort removes the invalidation handler and rejects', async () => {
        const c = new Computed<number>(makeKey('get'));
        c.setOutput(42);
        const ac = new AbortController();
        const caught = c.whenInvalidated(ac.signal).catch((e: unknown) => e);
        ac.abort(new Error('stop'));
        const err = await caught;
        expect(err).toBeInstanceOf(Error);
        const handlerCount = (
            c as unknown as { _onInvalidated: { count: number } }
        )._onInvalidated.count;
        expect(handlerCount).toBe(0);
        expect(() => c.invalidate()).not.toThrow();
    });
});

describe('K9 — update() is isolated from the ambient compute context', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('renewal inside a computing method creates no dependency edge', async () => {
        const store = new Map<string, number>();
        store.set('x', 1);
        class Svc {
            @computeMethod
            inner(key: string): number {
                return store.get(key)!;
            }
        }
        const svc = new Svc();

        const innerComputed = await Computed.capture(() => svc.inner('x'));
        innerComputed.invalidate();

        const outer = new Computed<number>(makeKey('outer'));
        const outerCtx = new ComputeContext(outer as Computed<unknown>);
        AsyncContext.current = new AsyncContext().with(
            computeContextKey,
            outerCtx
        );
        try {
            const renewed = await innerComputed.update();
            expect(renewed.isConsistent).toBe(true);
        } finally {
            AsyncContext.current = undefined;
        }

        expect(outer.dependencies.size).toBe(0);
    });
});
