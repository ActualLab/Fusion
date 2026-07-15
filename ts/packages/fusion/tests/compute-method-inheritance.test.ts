import { describe, it, expect } from 'vitest';
import { computeMethod, getMethodsMeta } from '../src/index.js';

describe('computeMethod metadata isolation across inheritance (R19)', () => {
    it('derived methods do not leak into the base contract', () => {
        class Base {
            @computeMethod
            base(_a: string): number { return 0; }
        }
        class Derived extends Base {
            @computeMethod
            derived(_a: string): number { return 0; }
        }

        expect(Object.keys(getMethodsMeta(Base) ?? {})).toEqual(['base']);
        expect(Object.keys(getMethodsMeta(Derived) ?? {}).sort()).toEqual(['base', 'derived']);
    });

    it('sibling derived contracts do not contaminate one another', () => {
        class Base {
            @computeMethod
            base(_a: string): number { return 0; }
        }
        class DerivedA extends Base {
            @computeMethod
            a(_a: string): number { return 0; }
        }
        class DerivedB extends Base {
            @computeMethod
            b(_a: string): number { return 0; }
        }

        expect(Object.keys(getMethodsMeta(Base) ?? {})).toEqual(['base']);
        expect(Object.keys(getMethodsMeta(DerivedA) ?? {}).sort()).toEqual(['a', 'base']);
        expect(Object.keys(getMethodsMeta(DerivedB) ?? {}).sort()).toEqual(['b', 'base']);
    });
});

describe('computeMethod wire arity (R20)', () => {
    it('infers argCount for a plain parameter list', () => {
        class Svc {
            @computeMethod
            m(_a: string, _b: number): number { return 0; }
        }
        expect(getMethodsMeta(Svc)!['m'].argCount).toBe(2);
    });

    it('throws for a default parameter without an explicit argCount', () => {
        expect(() => {
            class Svc {
                @computeMethod
                m(_a: string, _b = 1): number { return 0; }
            }
            return Svc;
        }).toThrow(/argCount/);
    });

    it('throws for a rest parameter without an explicit argCount', () => {
        expect(() => {
            class Svc {
                @computeMethod
                m(..._args: unknown[]): number { return 0; }
            }
            return Svc;
        }).toThrow(/argCount/);
    });

    it('uses the explicit argCount for a default parameter', () => {
        class Svc {
            @computeMethod({ argCount: 2 })
            m(_a: string, _b = 1): number { return 0; }
        }
        expect(getMethodsMeta(Svc)!['m'].argCount).toBe(2);
    });
});
