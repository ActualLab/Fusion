import { describe, it, expect } from 'vitest';
import { Computed, ComputedRegistry, ConsistencyState } from '../src/index.js';

let _testKeyCounter = 0;
function makeKey(method: string, ...args: unknown[]): string {
    return `Test.${method}[${++_testKeyCounter}]:${args.map(a => JSON.stringify(a)).join(',')}`;
}

describe('ComputedRegistry', () => {
    it('should register and retrieve computed via setOutput auto-registration', () => {
        const key = makeKey('get', 1);
        const computed = new Computed<number>(key);
        computed.setOutput(42);

        const retrieved = ComputedRegistry.get(key);
        expect(retrieved).toBe(computed);
    });

    it('should return undefined for missing input', () => {
        const key = makeKey('get', 999);
        expect(ComputedRegistry.get(key)).toBeUndefined();
    });

    it('should overwrite on re-register via setOutput', () => {
        const key = makeKey('get', 1);
        const c1 = new Computed<number>(key);
        c1.setOutput(1);

        const c2 = new Computed<number>(key);
        c2.setOutput(2);

        const retrieved = ComputedRegistry.get(key);
        expect(retrieved).toBe(c2);
    });

    it('should unregister entries', () => {
        const key = makeKey('get', 1);
        const computed = new Computed<number>(key);
        computed.setOutput(42);
        expect(ComputedRegistry.get(key)).toBe(computed);

        computed.invalidate();
        expect(ComputedRegistry.get(key)).toBeUndefined();
    });

    it('should invalidate a displaced still-consistent computed on re-register', () => {
        const key = makeKey('get', 1);
        const c1 = new Computed<number>(key);
        c1.setOutput(1);
        expect(c1.state).toBe(ConsistencyState.Consistent);

        const c2 = new Computed<number>(key);
        c2.setOutput(2);

        expect(c1.state).toBe(ConsistencyState.Invalidated);
        expect(ComputedRegistry.get(key)).toBe(c2);
    });

    it('should keep an already-registered computed intact on self re-register', () => {
        const key = makeKey('get', 1);
        const c1 = new Computed<number>(key);
        c1.setOutput(1);

        ComputedRegistry.register(c1 as Computed<unknown>);

        expect(c1.state).toBe(ConsistencyState.Consistent);
        expect(ComputedRegistry.get(key)).toBe(c1);
    });

    it('should track size', () => {
        const sizeBefore = ComputedRegistry.size;
        const key = makeKey('get', 1);
        const computed = new Computed<number>(key);
        computed.setOutput(42);
        expect(ComputedRegistry.size).toBe(sizeBefore + 1);

        computed.invalidate();
        expect(ComputedRegistry.size).toBe(sizeBefore);
    });
});
