import { describe, it, expect } from 'vitest';
import { abortSignalKey, AsyncContext, isCancellation } from '../src/index.js';

describe('isCancellation', () => {
    it('recognizes AbortError-named DOMException and Error', () => {
        const controller = new AbortController();
        controller.abort();
        expect(isCancellation(controller.signal.reason)).toBe(true);
        const e = new Error('x');
        e.name = 'AbortError';
        expect(isCancellation(e)).toBe(true);
    });

    it('rejects real failures and non-objects', () => {
        expect(isCancellation(new Error('boom'))).toBe(false);
        expect(isCancellation(undefined)).toBe(false);
        expect(isCancellation(null)).toBe(false);
        expect(isCancellation('AbortError')).toBe(false);
    });
});

describe('abortSignalKey', () => {
    it('should carry AbortSignal through context', () => {
        const controller = new AbortController();
        const ctx = new AsyncContext().with(abortSignalKey, controller.signal);
        expect(ctx.get(abortSignalKey)).toBe(controller.signal);
    });

    it('should default to undefined', () => {
        const ctx = new AsyncContext();
        expect(ctx.get(abortSignalKey)).toBeUndefined();
    });
});
