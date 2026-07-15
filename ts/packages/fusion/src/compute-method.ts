/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unsafe-member-access, @typescript-eslint/no-unsafe-argument, @typescript-eslint/no-unsafe-return, @typescript-eslint/no-this-alias */
import { ownMetadata, resolveArgCount } from '@actuallab/core';
import { ComputeFunction } from './compute-function.js';
import { ComputedRegistry } from './computed-registry.js';

const METHODS_META = Symbol.for('actuallab.methods');

export interface MethodMeta {
    argCount: number;
    compute?: boolean;
    stream?: boolean;
}

/** Read method metadata from a decorated class. */
export function getMethodsMeta(
    cls: abstract new (...args: any[]) => any
): Record<string, MethodMeta> | undefined {
    return (cls as any)[Symbol.metadata]?.[METHODS_META];
}

export interface ComputeMethodOptions {
    argCount?: number;
}

type ComputeMethodDecorator<This, Args extends unknown[], Return> = (
    target: (this: This, ...args: Args) => Return,
    context: ClassMethodDecoratorContext<This, (this: This, ...args: Args) => Return>
) => (this: This, ...args: Args) => Return;

/**
 * Method decorator — routes a method through ComputeFunction for caching and dependency
 * tracking. Usable bare (`@computeMethod`) or with options (`@computeMethod({ argCount })`).
 */
export function computeMethod<This, Args extends unknown[], Return>(
    target: (this: This, ...args: Args) => Return,
    context: ClassMethodDecoratorContext<This, (this: This, ...args: Args) => Return>
): (this: This, ...args: Args) => Return;
export function computeMethod<This, Args extends unknown[], Return>(
    options?: ComputeMethodOptions
): ComputeMethodDecorator<This, Args, Return>;
export function computeMethod<This, Args extends unknown[], Return>(
    targetOrOptions?: ((this: This, ...args: Args) => Return) | ComputeMethodOptions,
    context?: ClassMethodDecoratorContext<This, (this: This, ...args: Args) => Return>
): unknown {
    if (typeof targetOrOptions === 'function')
        return computeMethodImpl(targetOrOptions, context!, {});

    const options = targetOrOptions ?? {};
    return (target: (this: This, ...args: Args) => Return, ctx: ClassMethodDecoratorContext<This, (this: This, ...args: Args) => Return>) =>
        computeMethodImpl(target, ctx, options);
}

function computeMethodImpl<This, Args extends unknown[], Return>(
    target: (this: This, ...args: Args) => Return,
    context: ClassMethodDecoratorContext<
        This,
        (this: This, ...args: Args) => Return
    >,
    options: ComputeMethodOptions
): (this: This, ...args: Args) => Return {
    const methodName = String(context.name);

    const methods = ownMetadata<Record<string, MethodMeta>>(context.metadata, METHODS_META);
    methods[methodName] = {
        ...methods[methodName],
        compute: true,
        argCount: resolveArgCount(target, options.argCount, methodName),
    };

    // ONE ComputeFunction per class×method — created at decoration time
    const cf = new ComputeFunction(methodName, target as any);

    // Prototype-level replacement — unwraps Computed to return the value directly
    const replacement = function (this: This, ...allArgs: Args): Return {
        return cf.invoke(this as object, allArgs).then(c => c.value) as Return;
    };

    // Per-instance setup: create bound method with .invalidate pre-bound
    context.addInitializer(function (this: This) {
        const instance = this;
        const boundMethod = (...allArgs: unknown[]) => {
            return cf.invoke(instance as object, allArgs).then(c => c.value);
        };
        (boundMethod as any).invalidate = (...args: unknown[]) => {
            const key = cf.buildKey(instance as object, args);
            ComputedRegistry.get(key)?.invalidate();
        };
        (this as any)[methodName] = boundMethod;
    });

    return replacement;
}

/** Wrap a standalone function as a compute function with caching and .invalidate(). */
export function wrapComputeMethod<Args extends unknown[], Return>(
    fn: (...args: Args) => Return
): ((...args: Args) => Promise<Return>) & {
    invalidate: (...args: Args) => void;
} {
    const syntheticInstance = {};
    const methodName = fn.name || 'anonymous';
    const cf = new ComputeFunction(methodName, fn as any);

    const wrapped = (...allArgs: unknown[]) => {
        return cf
            .invoke(syntheticInstance, allArgs)
            .then(c => c.value as Return);
    };

    (wrapped as any).invalidate = (...args: unknown[]) => {
        const key = cf.buildKey(syntheticInstance, args);
        ComputedRegistry.get(key)?.invalidate();
    };

    return wrapped as any;
}
