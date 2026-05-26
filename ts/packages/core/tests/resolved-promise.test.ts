import { describe, it, expect } from 'vitest';
import { ResolvedPromise, TimedOut } from '../src/index.js';

describe('ResolvedPromise', () => {
    it('Void resolves without throwing', async () => {
        await ResolvedPromise.Void;
    });

    it('True resolves to true', async () => {
        expect(await ResolvedPromise.True).toBe(true);
    });

    it('False resolves to false', async () => {
        expect(await ResolvedPromise.False).toBe(false);
    });

    it('returns the same promise instance across reads', () => {
        expect(ResolvedPromise.Void).toBe(ResolvedPromise.Void);
        expect(ResolvedPromise.True).toBe(ResolvedPromise.True);
        expect(ResolvedPromise.False).toBe(ResolvedPromise.False);
    });
});

describe('TimedOut', () => {
    it('exposes a stable singleton via .instance', () => {
        expect(TimedOut.instance).toBe(TimedOut.instance);
    });

    it('is an instance of TimedOut', () => {
        expect(TimedOut.instance).toBeInstanceOf(TimedOut);
    });
});
