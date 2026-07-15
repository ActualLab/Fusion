/* eslint-disable @typescript-eslint/await-thenable -- @computeMethod decorator wraps methods to return Promise at runtime */
import { describe, it, expect, beforeEach } from 'vitest';
import { AsyncContext, PromiseSource } from '@actuallab/core';
import {
    computeMethod,
    wrapComputeMethod,
    getMethodsMeta,
    MutableState,
} from '../src/index.js';

describe('@computeMethod decorator', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('should cache results for same args on same instance', async () => {
        let callCount = 0;

        class Svc {
            @computeMethod
            getValue(id: string): number {
                callCount++;
                return id.length;
            }
        }

        const svc = new Svc();
        const r1 = await svc.getValue('hello');
        const r2 = await svc.getValue('hello');

        expect(r1).toBe(5);
        expect(r2).toBe(5);
        expect(callCount).toBe(1); // cached
    });

    it('should produce different results for different args', async () => {
        class Svc {
            @computeMethod
            getValue(id: string): number {
                return id.length;
            }
        }

        const svc = new Svc();
        expect(await svc.getValue('ab')).toBe(2);
        expect(await svc.getValue('abc')).toBe(3);
    });

    it('should recompute after invalidation', async () => {
        let counter = 0;

        class Svc {
            @computeMethod
            getCount(_id: string): number {
                return ++counter;
            }
        }

        const svc = new Svc();
        expect(await svc.getCount('x')).toBe(1);
        expect(await svc.getCount('x')).toBe(1); // cached

        // eslint-disable-next-line @typescript-eslint/no-explicit-any, @typescript-eslint/no-unsafe-member-access, @typescript-eslint/no-unsafe-call
        (svc.getCount as any).invalidate('x');

        expect(await svc.getCount('x')).toBe(2); // recomputed
    });

    it("should preserve 'this' binding", async () => {
        class Svc {
            private store = new Map<string, number>();

            @computeMethod
            getValue(id: string): number {
                return this.store.get(id) ?? 0;
            }

            setStore(id: string, value: number) {
                this.store.set(id, value);
            }
        }

        const svc = new Svc();
        svc.setStore('a', 42);
        expect(await svc.getValue('a')).toBe(42);
    });

    it('should keep different instances independent', async () => {
        class Svc {
            constructor(private prefix: string) {}

            @computeMethod
            getValue(id: string): string {
                return `${this.prefix}-${id}`;
            }
        }

        const svc1 = new Svc('A');
        const svc2 = new Svc('B');

        expect(await svc1.getValue('x')).toBe('A-x');
        expect(await svc2.getValue('x')).toBe('B-x');
    });

    it('should store metadata via Symbol.metadata', () => {
        class Svc {
            @computeMethod
            getValue(_id: string): number {
                return 0;
            }

            @computeMethod
            getOther(_a: string, _b: number): string {
                return '';
            }
        }

        const meta = getMethodsMeta(Svc);
        expect(meta).toBeDefined();
        expect(meta!['getValue']).toEqual({ compute: true, argCount: 1 });
        expect(meta!['getOther']).toEqual({ compute: true, argCount: 2 });
    });

    it('should handle errors in compute methods', async () => {
        class Svc {
            @computeMethod
            getValue(_id: string): number {
                throw new Error('compute error');
            }
        }

        const svc = new Svc();
        await expect(svc.getValue('x')).rejects.toThrow('compute error');
    });
});

describe('wrapComputeMethod', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('should cache standalone function results', async () => {
        let callCount = 0;
        const computeValue = wrapComputeMethod((id: string) => {
            callCount++;
            return id.length;
        });

        expect(await computeValue('hello')).toBe(5);
        expect(await computeValue('hello')).toBe(5);
        expect(callCount).toBe(1);
    });

    it('should support invalidation', async () => {
        let counter = 0;
        const computeValue = wrapComputeMethod((_id: string) => {
            return ++counter;
        });

        expect(await computeValue('x')).toBe(1);

        computeValue.invalidate('x');

        expect(await computeValue('x')).toBe(2);
    });
});

describe('compute method invalidation vs. in-flight computation', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    // K2: a Computed is registered while still Computing, so invalidate() during
    // the computation lands as pending and the next call recomputes.
    it('should not lose invalidate() called during an in-flight computation', async () => {
        let store = 1;
        const gate = new PromiseSource<void>();
        const started = new PromiseSource<void>();
        const getValue = wrapComputeMethod(async function getValue() {
            const v = store;
            started.resolve(undefined);
            await gate;
            return v;
        });

        const p = getValue();
        await started;

        store = 2;
        getValue.invalidate();
        gate.resolve(undefined);

        expect(await p).toBe(1);
        expect(await getValue()).toBe(2);
    });

    // K1: addDependency on an already-invalidated dependency invalidates the
    // (still Computing) dependant instead of adding a dead edge.
    it('should invalidate a dependant that captures an already-invalidated dependency', async () => {
        const s = new MutableState(1);
        const gate = new PromiseSource<void>();
        const started = new PromiseSource<void>();
        const child = wrapComputeMethod(async function child() {
            const v = s.use();
            started.resolve(undefined);
            await gate;
            return v;
        });
        const parent = wrapComputeMethod(function parent() {
            return child();
        });

        const p = parent();
        await started;

        // Invalidate the child (via its dependency) while it is still computing;
        // it self-invalidates right after setOutput, before the parent captures it
        s.set(2);
        gate.resolve(undefined);

        expect(await p).toBe(1);
        expect(await parent()).toBe(2);
    });
});
