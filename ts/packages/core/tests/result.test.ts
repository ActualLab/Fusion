import { describe, it, expect } from 'vitest';
import {
    Result,
    result,
    errorResult,
    resultFrom,
    resultFromAsync,
} from '../src/index.js';

describe('Result', () => {
    it('result should create a successful result', () => {
        const r = result(42);
        expect(r.hasValue).toBe(true);
        expect(r.value).toBe(42);
    });

    it('errorResult should create a failed result', () => {
        const r = errorResult(new Error('fail'));
        expect(r.hasValue).toBe(false);
        expect(r.error).toBeInstanceOf(Error);
    });

    it('resultFrom should capture success', () => {
        const r = resultFrom(() => 42);
        expect(r.hasValue).toBe(true);
        expect(r.value).toBe(42);
    });

    it('resultFrom should capture thrown error', () => {
        const r = resultFrom(() => {
            throw new Error('boom');
        });
        expect(r.hasError).toBe(true);
        expect((r.error as Error).message).toBe('boom');
    });

    it('resultFromAsync should capture async success', async () => {
        const r = await resultFromAsync(() => Promise.resolve(42));
        expect(r.hasValue).toBe(true);
        expect(r.value).toBe(42);
    });

    it('resultFromAsync should capture async error', async () => {
        const r = await resultFromAsync(() => {
            return Promise.reject(new Error('async boom'));
        });
        expect(r.hasError).toBe(true);
    });

    it('value should throw on error result', () => {
        const r = errorResult(new Error('fail'));
        expect(() => r.value).toThrow('fail');
    });

    it('valueOrUndefined should return undefined on error', () => {
        const r = errorResult(new Error('fail'));
        expect(r.valueOrUndefined).toBeUndefined();
    });

    it('errorResult(undefined) stays an error, normalized (C2)', () => {
        const r = errorResult<number>(undefined);
        expect(r.hasError).toBe(true);
        expect(r.hasValue).toBe(false);
        expect(r.error).toBeInstanceOf(Error);
        expect(() => r.value).toThrow('Unspecified error');
    });

    it('errorResult(null) stays an error, normalized (C2)', () => {
        const r = errorResult<number>(null);
        expect(r.hasError).toBe(true);
        expect(r.error).toBeInstanceOf(Error);
    });

    it('resultFrom capturing "throw undefined" yields an error result (C2)', () => {
        const r = resultFrom<number>(() => {
            // eslint-disable-next-line @typescript-eslint/only-throw-error
            throw undefined;
        });
        expect(r.hasError).toBe(true);
        expect(r.error).toBeInstanceOf(Error);
    });

    it('equals compares values with Object.is and errors by reference (C9)', () => {
        expect(result(42).equals(result(42))).toBe(true);
        expect(result(42).equals(result(43))).toBe(false);
        expect(result(NaN).equals(result(NaN))).toBe(true);

        const e = new Error('boom');
        expect(errorResult<number>(e).equals(errorResult<number>(e))).toBe(true);
        expect(errorResult<number>(new Error('boom')).equals(errorResult<number>(new Error('boom')))).toBe(false);

        expect(result(1).equals(errorResult<number>(e))).toBe(false);
    });

    it('equals treats a hasError/hasValue mismatch as inequality (C9)', () => {
        const forged = new Result<number>(42, undefined, true);
        expect(forged.equals(result(42))).toBe(false);
        expect(result(42).equals(forged)).toBe(false);
    });

    it('equals accepts a custom value comparer (C9)', () => {
        const a = result({ id: 1 });
        const b = result({ id: 1 });
        expect(a.equals(b)).toBe(false);
        expect(a.equals(b, (x, y) => x.id === y.id)).toBe(true);
    });
});
