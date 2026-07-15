import { describe, it, expect } from 'vitest';
import {
    rpcService,
    rpcMethod,
    getServiceMeta,
    getMethodsMeta,
} from '../src/index.js';

describe('rpcMethod metadata isolation across inheritance (R19)', () => {
    it('derived methods do not leak into the base contract', () => {
        class Base {
            @rpcMethod()
            base(_a: string): void { /* noop */ }
        }
        class Derived extends Base {
            @rpcMethod()
            derived(_a: string): void { /* noop */ }
        }

        expect(Object.keys(getMethodsMeta(Base) ?? {})).toEqual(['base']);
        expect(Object.keys(getMethodsMeta(Derived) ?? {}).sort()).toEqual(['base', 'derived']);
    });

    it('decorating a derived service does not rename the base service', () => {
        @rpcService('Base')
        class Base {
            @rpcMethod()
            base(_a: string): void { /* noop */ }
        }
        @rpcService('Derived')
        class Derived extends Base {
            @rpcMethod()
            derived(_a: string): void { /* noop */ }
        }

        expect(getServiceMeta(Base)!.name).toBe('Base');
        expect(getServiceMeta(Derived)!.name).toBe('Derived');
    });

    it('sibling derived contracts do not contaminate one another', () => {
        class Base {
            @rpcMethod()
            base(_a: string): void { /* noop */ }
        }
        class DerivedA extends Base {
            @rpcMethod()
            a(_a: string): void { /* noop */ }
        }
        class DerivedB extends Base {
            @rpcMethod()
            b(_a: string): void { /* noop */ }
        }

        expect(Object.keys(getMethodsMeta(Base) ?? {})).toEqual(['base']);
        expect(Object.keys(getMethodsMeta(DerivedA) ?? {}).sort()).toEqual(['a', 'base']);
        expect(Object.keys(getMethodsMeta(DerivedB) ?? {}).sort()).toEqual(['b', 'base']);
    });
});

describe('rpcMethod wire arity (R20)', () => {
    it('infers argCount for a plain parameter list', () => {
        class Svc {
            @rpcMethod()
            m(_a: string, _b: number): void { /* noop */ }
        }
        expect(getMethodsMeta(Svc)!['m'].argCount).toBe(2);
    });

    it('throws for a default parameter without an explicit argCount', () => {
        expect(() => {
            class Svc {
                @rpcMethod()
                m(_a: string, _b = 1): void { /* noop */ }
            }
            return Svc;
        }).toThrow(/argCount/);
    });

    it('throws for a rest parameter without an explicit argCount', () => {
        expect(() => {
            class Svc {
                @rpcMethod()
                m(..._args: unknown[]): void { /* noop */ }
            }
            return Svc;
        }).toThrow(/argCount/);
    });

    it('uses the explicit argCount for a default parameter', () => {
        class Svc {
            @rpcMethod({ argCount: 2 })
            m(_a: string, _b = 1): void { /* noop */ }
        }
        expect(getMethodsMeta(Svc)!['m'].argCount).toBe(2);
    });
});
