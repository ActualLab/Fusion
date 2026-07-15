// Regression tests for docs/plans/ts-port-audit.md item S12 — lastNonErrorValue is
// stored as a reference to the last non-error computed, so a legitimate `undefined`
// value is distinguishable from "no non-error value yet".
import { describe, it, expect, beforeEach } from 'vitest';
import { AsyncContext } from '@actuallab/core';
import { ComputedState, FixedDelayer } from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('lastNonErrorValue with undefined values (S12)', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('an undefined non-error value survives a later error output', async () => {
        let phase = 0;
        const state = new ComputedState<number | undefined>(
            () => {
                phase++;
                if (phase === 1)
                    return undefined;

                throw new Error('boom');
            },
            { updateDelayer: FixedDelayer.zero }
        );

        await state.whenFirstTimeUpdated();
        expect(state.hasValue).toBe(true);
        expect(state.value).toBeUndefined();
        expect(state.lastNonErrorValue).toBeUndefined();

        state.computed.invalidate();
        await delay(20);
        expect(state.hasError).toBe(true);

        // value rethrows the stored error (base-class parity, S2); the last non-error
        // value stays reachable only through the explicit lastNonErrorValue getter, and
        // here it is a legitimate `undefined` distinguished from "no non-error value yet".
        expect(() => state.value).toThrow('boom');
        expect(state.valueOrUndefined).toBeUndefined();
        expect(state.lastNonErrorValue).toBeUndefined();
    });

    it('valueOrUndefined reflects a legitimately undefined current value', async () => {
        let phase = 0;
        const state = new ComputedState<number | undefined>(
            () => {
                phase++;
                return phase === 1 ? 5 : undefined;
            },
            { updateDelayer: FixedDelayer.zero }
        );

        await state.whenFirstTimeUpdated();
        expect(state.value).toBe(5);

        state.computed.invalidate();
        await delay(20);

        // The current computed holds a real `undefined`; the old `?? lastNonErrorValue`
        // fallback masked it as 5.
        expect(state.hasValue).toBe(true);
        expect(state.value).toBeUndefined();
        expect(state.valueOrUndefined).toBeUndefined();
        expect(state.lastNonErrorValue).toBeUndefined();
    });
});
