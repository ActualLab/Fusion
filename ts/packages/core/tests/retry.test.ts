import { describe, it, expect } from 'vitest';
import { retry, catchErrors } from '../src/index.js';

describe('retry', () => {
    it('returns the first successful result', async () => {
        let attempts = 0;
        const result = await retry(3, () => { attempts++; return 42; });
        expect(result).toBe(42);
        expect(attempts).toBe(1);
    });

    it('retries on failure up to tryCount times', async () => {
        let attempts = 0;
        const result = await retry(3, (i, lastErr) => {
            attempts++;
            // Caller can inspect the previous error
            if (i === 0) expect(lastErr).toBeUndefined();
            else expect(lastErr).toBeInstanceOf(Error);
            if (i < 2) throw new Error(`fail-${i}`);
            return 'ok';
        }, () => 0);
        expect(result).toBe('ok');
        expect(attempts).toBe(3);
    });

    it('throws the last error after exhausting tryCount', async () => {
        let attempts = 0;
        await expect(retry(2, () => { attempts++; throw new Error(`fail-${attempts}`); }, () => 0))
            .rejects.toThrow('fail-2');
        expect(attempts).toBe(2);
    });

    it('rejects tryCount <= 0', async () => {
        await expect(retry(0, () => 1)).rejects.toThrow('tryCount must be positive');
    });

    it('honors the retry-delay schedule', async () => {
        const sleeps: number[] = [];
        const schedule = (i: number): number => {
            const ms = i * 5;
            sleeps.push(ms);
            return ms;
        };
        let attempts = 0;
        await retry(3, () => {
            attempts++;
            if (attempts < 3) throw new Error('keep trying');
            return 'done';
        }, schedule);
        // Schedule is invoked with the *upcoming* retry index (1, 2)
        expect(sleeps).toEqual([5, 10]);
    });

    it('aborts the inter-attempt delay via the signal', async () => {
        const ac = new AbortController();
        const reason = new Error('stop retrying');
        let attempts = 0;
        const promise = retry(5, () => {
            attempts++;
            ac.abort(reason);
            throw new Error('keep trying');
        }, () => 10_000, ac.signal);

        await expect(promise).rejects.toBe(reason);
        expect(attempts).toBe(1);
    });
});

describe('catchErrors', () => {
    it('returns the resolved value on success', async () => {
        expect(await catchErrors(() => 7)).toBe(7);
    });

    it('returns undefined on error when no handler given', async () => {
        const result = await catchErrors<number>(() => { throw new Error('x'); });
        expect(result).toBeUndefined();
    });

    it('invokes onError with the thrown value', async () => {
        let captured: unknown;
        const result = await catchErrors<string>(
            () => { throw new Error('boom'); },
            e => { captured = e; return 'recovered'; },
        );
        expect(result).toBe('recovered');
        expect((captured as Error).message).toBe('boom');
    });

    it('awaits the inner async function', async () => {
        const result = await catchErrors(async () => {
            await new Promise(r => setTimeout(r, 5));
            return 'late';
        });
        expect(result).toBe('late');
    });
});
