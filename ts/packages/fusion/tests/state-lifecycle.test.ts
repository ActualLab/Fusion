// Regression tests for docs/plans/ts-port-audit.md items S11 (versioned whenUpdated)
// and S5 (prompt termination on dispose mid-computation). Each asserts the C# contract.
import { describe, it, expect, beforeEach } from 'vitest';
import { AsyncContext, PromiseSource } from '@actuallab/core';
import { ComputedState, MutableState } from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('State lifecycle (S11, S5)', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('S11: whenUpdated(sinceIndex) resolves immediately for a missed generation', async () => {
        const s = new MutableState(1);
        const sinceIndex = s.updateIndex;
        s.set(2); // advances updateIndex past sinceIndex

        // Subscribe AFTER the update with the stale index — must resolve at once
        await expect(s.whenUpdated(sinceIndex)).resolves.toBeUndefined();
    });

    it('S11: whenUpdated() with no missed generation waits for the next update', async () => {
        const s = new MutableState(1);
        let resolved = false;
        const p = s.whenUpdated().then(() => void (resolved = true));

        await delay(10);
        expect(resolved).toBe(false);

        s.set(2);
        await p;
        expect(resolved).toBe(true);
    });

    it('S11: whenUpdated rejects outstanding and future waits on dispose', async () => {
        const gate = new PromiseSource<void>();
        const state = new ComputedState<number>(async () => {
            await gate;
            return 1;
        });
        const outstanding = state.whenUpdated();
        state.dispose();

        await expect(outstanding).rejects.toThrow(/disposed/i);
        await expect(state.whenUpdated()).rejects.toThrow(/disposed/i);
        await expect(state.whenFirstTimeUpdated()).rejects.toThrow(/disposed/i);
    });

    it('S5: waits on a disposed-mid-computation state settle instead of hanging', async () => {
        const dep = new MutableState<number>(1);
        const gate = new PromiseSource<void>();
        const started = new PromiseSource<void>();
        const state = new ComputedState<number>(async () => {
            const v = dep.use(); // sync prefix — dependency captured
            started.resolve(undefined);
            await gate;
            return v;
        });

        await started;
        state.dispose();
        gate.resolve(undefined);
        await delay(10); // let the cycle finish its in-flight iteration
        dep.set(2); // invalidates whatever the dead cycle published

        // C# contract: update()/use() on a disposed state fails fast
        // (cancellation) — it never hangs forever
        const outcome = await Promise.race([
            Promise.resolve(state.update()).then(
                () => 'settled',
                () => 'settled'
            ),
            delay(500).then(() => 'timeout'),
        ]);
        expect(outcome).toBe('settled');
    });

    it('S5: dispose mid-computation publishes nothing', async () => {
        const dep = new MutableState<number>(1);
        const gate = new PromiseSource<void>();
        const started = new PromiseSource<void>();
        const state = new ComputedState<number>(async () => {
            const v = dep.use();
            started.resolve(undefined);
            await gate;
            return v;
        });

        await started;
        state.dispose();
        gate.resolve(undefined);
        await delay(10);

        // The in-flight iteration returned after dispose — it must skip _update
        expect(state.updateIndex).toBe(0);
    });
});
