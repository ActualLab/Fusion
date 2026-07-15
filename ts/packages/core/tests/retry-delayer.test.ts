// Regression tests guarding the verified C# parity of RetryDelaySeq /
// RetryDelayer (see docs/plans/ts-port-audit.md, "Parity confirmed (core)").
// The abort-handling gap is item C3, covered by ts-port-audit-repro.test.ts.
import { describe, it, expect } from 'vitest';
import {
    RetryDelayer,
    RetryDelayNone,
    RetryDelaySeq,
} from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('RetryDelaySeq', () => {
    it('returns 0 for failureCount <= 0', () => {
        const s = RetryDelaySeq.exp(1000, 60_000, 0);
        expect(s.getDelay(0)).toBe(0);
        expect(s.getDelay(-1)).toBe(0);
    });

    it('fixed() yields a constant delay (spread 0)', () => {
        const s = RetryDelaySeq.fixed(1000, 0);
        expect(s.getDelay(1)).toBe(1000);
        expect(s.getDelay(5)).toBe(1000);
        expect(s.getDelay(100)).toBe(1000);
    });

    it('exp() grows as min * multiplier^(n-1), capped by max (spread 0)', () => {
        const s = RetryDelaySeq.exp(1000, 4000, 0, 2);
        expect(s.getDelay(1)).toBe(1000);
        expect(s.getDelay(2)).toBe(2000);
        expect(s.getDelay(3)).toBe(4000);
        expect(s.getDelay(4)).toBe(4000); // capped
    });

    it('spread jitters within origin ± origin*spread', () => {
        const s = RetryDelaySeq.fixed(1000, 0.1);
        for (let i = 0; i < 20; i++) {
            const d = s.getDelay(1);
            expect(d).toBeGreaterThanOrEqual(900);
            expect(d).toBeLessThanOrEqual(1100);
        }
    });
});

describe('RetryDelayer', () => {
    it('returns no delay for tryIndex 0', () => {
        const d = new RetryDelayer();
        expect(d.getDelay(0)).toBe(RetryDelayNone);
    });

    it('reports limit exceeded at the limit', () => {
        const d = new RetryDelayer();
        d.limit = 3;
        expect(d.getDelay(2).isLimitExceeded).toBe(false);
        expect(d.getDelay(3).isLimitExceeded).toBe(true);
        expect(d.getDelay(4).isLimitExceeded).toBe(true);
    });

    it('cancelDelays() resolves a pending delay promptly (no rejection)', async () => {
        const d = new RetryDelayer();
        d.delays = RetryDelaySeq.fixed(30_000, 0);
        const retryDelay = d.getDelay(1);
        expect(retryDelay.endsAt).toBeGreaterThan(Date.now());

        d.cancelDelays();

        const outcome = await Promise.race([
            retryDelay.promise.then(() => 'resolved'),
            delay(300).then(() => 'timeout'),
        ]);
        expect(outcome).toBe('resolved');
    });

    it('delays created after cancelDelays() still run (controller swap)', async () => {
        const d = new RetryDelayer();
        d.delays = RetryDelaySeq.fixed(50, 0);
        d.cancelDelays();

        const retryDelay = d.getDelay(1);
        const outcome = await Promise.race([
            retryDelay.promise.then(() => 'resolved'),
            delay(500).then(() => 'timeout'),
        ]);
        expect(outcome).toBe('resolved');
    });
});
