import { AsyncContext, AsyncContextKey } from '@actuallab/core';
import type { Computed } from './computed.js';

export const computeContextKey = new AsyncContextKey<
    ComputeContext | undefined
>('ComputeContext', undefined);

/**
 * Tracks the currently executing (or captured) computation and captures dependencies.
 * A context with a `computed` is a computing context (adds dependant edges); one without
 * is a capture context (records the last captured Computed only — no edges).
 */
export class ComputeContext {
    readonly computed: Computed<unknown> | undefined;
    readonly parent: ComputeContext | undefined;
    private _isCapturing = true;
    private _captured: Computed<unknown> | undefined;

    constructor(
        computed?: Computed<unknown>,
        parent?: ComputeContext
    ) {
        this.computed = computed;
        this.parent = parent;
    }

    get isCapturing(): boolean {
        return this._isCapturing;
    }

    get captured(): Computed<unknown> | undefined {
        return this._captured;
    }

    stopCapturing(): void {
        this._isCapturing = false;
    }

    captureDependency(dependency: Computed<unknown>): void {
        if (!this._isCapturing) return;
        // Last capture wins — C# ComputeContext.TryCapture overwrites _captured.
        this._captured = dependency;
        this.computed?.addDependency(dependency);
    }

    // Walks the caller chain for a computation still in progress under the same key —
    // C# AsyncLockSet(LockReentryMode.CheckedFail) analog, used to fail fast on reentry.
    // Only Computing ancestors count: a context leaked past its finished computation
    // (e.g. via AsyncLocalStorage into a later callback) must not fail later calls.
    hasKeyInChain(key: unknown): boolean {
        const computed = this.computed;
        if (computed?.isComputing === true && computed.input === key)
            return true;

        return this.parent?.hasKeyInChain(key) ?? false;
    }

    /** Resolve ComputeContext from an AsyncContext (or current). */
    static from(ctx: AsyncContext | undefined): ComputeContext | undefined {
        return AsyncContext.from(ctx)?.get(computeContextKey);
    }
}
