import { describe, it, expect, beforeEach } from 'vitest';
import { AsyncContext } from '@actuallab/core';
import { ComputedState, FixedDelayer } from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('ComputedState', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('should compute initial value', async () => {
        const state = new ComputedState<number>(() => 42);

        await state.whenFirstTimeUpdated();
        expect(state.updateIndex).toBe(1);
        expect(state.value).toBe(42);
    });

    it('should auto-update on invalidation with zero delayer', async () => {
        let counter = 0;
        const state = new ComputedState<number>(() => ++counter, {
            updateDelayer: FixedDelayer.zero,
        });

        await state.whenFirstTimeUpdated();
        expect(state.value).toBe(1);

        state.computed.invalidate();
        await delay(20);
        expect(state.updateIndex).toBe(2);
        expect(state.value).toBe(2);
    });

    it('should auto-update on invalidation with FixedDelayer', async () => {
        let counter = 0;
        const state = new ComputedState<number>(() => ++counter, {
            updateDelayer: new FixedDelayer(30).delay,
        });

        await state.whenFirstTimeUpdated();
        expect(state.value).toBe(1);

        state.computed.invalidate();

        // Should not have updated yet
        await delay(10);
        expect(state.value).toBe(1);

        // Wait for delay + update
        await delay(50);
        expect(state.value).toBe(2);
    });

    it('should recompute after invalidation and resolve whenUpdated', async () => {
        let counter = 0;
        const state = new ComputedState<number>(() => ++counter, {
            updateDelayer: FixedDelayer.zero,
        });

        await state.whenFirstTimeUpdated();
        expect(state.updateIndex).toBe(1);

        const updated = state.whenUpdated();
        state.computed.invalidate();
        await updated;

        expect(state.updateIndex).toBe(2);
        expect(state.value).toBe(2);
    });

    it('should track lastNonErrorValue', async () => {
        let counter = 0;
        const state = new ComputedState<number>(
            () => {
                counter++;
                if (counter === 2) throw new Error('transient');
                return counter;
            },
            { updateDelayer: FixedDelayer.zero }
        );

        await state.whenFirstTimeUpdated();
        expect(state.value).toBe(1);
        expect(state.lastNonErrorValue).toBe(1);

        state.computed.invalidate();
        await delay(20);

        expect(state.error).toBeDefined();
        expect(state.lastNonErrorValue).toBe(1);
    });

    it('should not update after dispose', async () => {
        let counter = 0;
        const state = new ComputedState<number>(() => ++counter, {
            updateDelayer: FixedDelayer.zero,
        });

        await state.whenFirstTimeUpdated();
        const computed = state.computed;

        state.dispose();
        expect(state.isDisposed).toBe(true);

        computed.invalidate();
        await delay(20);

        expect(counter).toBe(1);
    });

    it('should accept initialValue option for async computers', async () => {
        let resolveComputer!: (v: number) => void;
        const state = new ComputedState<number>(
            () =>
                new Promise<number>(r => {
                    resolveComputer = r;
                }),
            { initialValue: 99 }
        );

        expect(state.updateIndex).toBe(0);
        expect(state.value).toBe(99);

        resolveComputer(42);
        await state.whenFirstTimeUpdated();
        expect(state.updateIndex).toBe(1);
        expect(state.value).toBe(42);
    });

    it('update() should return current computed when consistent', async () => {
        const state = new ComputedState<number>(() => 42);
        await state.whenFirstTimeUpdated();

        const result = state.update();
        expect(result).toBe(state.computed);
        expect(state.computed.isConsistent).toBe(true);
    });

    it('update() should trigger renewer and wait for recomputation', async () => {
        let counter = 0;
        const state = new ComputedState<number>(() => ++counter, {
            updateDelayer: FixedDelayer.zero,
        });
        await state.whenFirstTimeUpdated();
        expect(state.value).toBe(1);

        state.computed.invalidate();
        const updated = state.update();
        // ComputedState renewer is async — returns a Promise
        expect(updated).toBeInstanceOf(Promise);
        const renewed = await updated;
        expect(renewed.isConsistent).toBe(true);
        expect(state.value).toBe(2);
    });

    it('recompute() should invalidate and wait for new value', async () => {
        let counter = 0;
        const state = new ComputedState<number>(() => ++counter, {
            updateDelayer: FixedDelayer.zero,
        });
        await state.whenFirstTimeUpdated();
        expect(state.value).toBe(1);

        const renewed = await state.recompute();
        expect(renewed.isConsistent).toBe(true);
        expect(state.value).toBe(2);
        expect(state.updateIndex).toBe(2);
    });
});

describe('ComputedState audit fixes', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('S1: an initial-value ComputedState is pre-invalidated; use() awaits the real value', async () => {
        let resolveComputer!: (v: number) => void;
        const state = new ComputedState<number>(
            () =>
                new Promise<number>(r => {
                    resolveComputer = r;
                }),
            { initialValue: 99, updateDelayer: FixedDelayer.zero }
        );

        // C# State.cs:246 parity — the initial computed is created pre-invalidated,
        // so a dependant captures an invalidated computed, not the consistent placeholder.
        expect(state.computed.isConsistent).toBe(false);
        expect(state.value).toBe(99); // placeholder still readable directly

        const p = state.use() as Promise<number>;
        expect(p).toBeInstanceOf(Promise);
        resolveComputer(42);
        expect(await p).toBe(42); // the real value, never the placeholder
    });

    it('S13: use() on a fresh async ComputedState awaits instead of crashing', async () => {
        let resolveComputer!: (v: number) => void;
        const state = new ComputedState<number>(
            () =>
                new Promise<number>(r => {
                    resolveComputer = r;
                }),
            { updateDelayer: FixedDelayer.zero }
        );

        // Previously a TypeError (inherited use() dereferenced an unset _computed).
        const p = state.use();
        expect(p).toBeInstanceOf(Promise);

        resolveComputer(7);
        expect(await (p as Promise<number>)).toBe(7);
    });

    it('S13: update() on a fresh async ComputedState awaits the first computation', async () => {
        let resolveComputer!: (v: number) => void;
        const state = new ComputedState<number>(
            () =>
                new Promise<number>(r => {
                    resolveComputer = r;
                }),
            { updateDelayer: FixedDelayer.zero }
        );

        const updated = state.update();
        expect(updated).toBeInstanceOf(Promise);

        resolveComputer(5);
        const computed = await updated;
        expect(computed.value).toBe(5);
    });

    it('S2: an error output rethrows through value / reflects in output, no masking', async () => {
        const boom = new Error('transient');
        let counter = 0;
        const state = new ComputedState<number>(
            () => {
                counter++;
                if (counter === 2) throw boom;
                return counter;
            },
            { updateDelayer: FixedDelayer.zero }
        );

        await state.whenFirstTimeUpdated();
        expect(state.value).toBe(1);

        state.computed.invalidate();
        await delay(20);

        expect(state.hasValue).toBe(false);
        expect(state.hasError).toBe(true);
        // value rethrows the stored error instead of masking it with the stale value.
        expect(() => state.value).toThrow(boom);
        // valueOrUndefined stays base-consistent (error -> undefined, C# ValueOrDefault).
        expect(state.valueOrUndefined).toBeUndefined();
        expect(state.output.hasError).toBe(true);
        expect(state.output.error).toBe(boom);
        // The stale value is reachable only through the explicit getter.
        expect(state.lastNonErrorValue).toBe(1);
    });

    it('S2: a first computation that errors has no masking source', async () => {
        const boom = new Error('boom');
        const state = new ComputedState<number>(
            () => {
                throw boom;
            },
            { updateDelayer: FixedDelayer.zero }
        );

        await state.whenFirstTimeUpdated();
        expect(state.hasValue).toBe(false);
        expect(state.hasError).toBe(true);
        expect(() => state.value).toThrow(boom);
        expect(state.valueOrUndefined).toBeUndefined();
        // The pre-invalidated initial computed is the last non-error computed,
        // so lastNonErrorValue is the placeholder — C# default(T) / initialValue parity.
        expect(state.lastNonErrorValue).toBeUndefined();

        const withInitial = new ComputedState<number>(
            () => {
                throw boom;
            },
            { initialValue: 7, updateDelayer: FixedDelayer.zero }
        );

        await withInitial.whenFirstTimeUpdated();
        expect(() => withInitial.value).toThrow(boom);
        expect(withInitial.valueOrUndefined).toBeUndefined();
        expect(withInitial.lastNonErrorValue).toBe(7);
    });
});
