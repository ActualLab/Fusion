import {
    AsyncContext,
    EventHandlerSet,
    type IResult,
    isCancellation,
    PromiseSource,
    Result,
    result,
} from '@actuallab/core';
import type { ComputedInput } from './computed-input.js';
import { ComputedRegistry } from './computed-registry.js';
import { ComputeContext, computeContextKey } from './compute-context.js';
import { ComputedOptions } from './computed-options.js';
import { getLogs } from './logging.js';
import type { State } from './state.js';

const { errorLog } = getLogs('Computed');

let _nextVersion = 0;

export const enum ConsistencyState {
    Computing = 0,
    Consistent = 1,
    Invalidated = 2,
}

/** Core Fusion abstraction — a cached computation with dependency tracking and invalidation. */
export class Computed<T> implements IResult<T> {
    /** Capture the Computed backing a computation — works with both local and RPC compute methods. */
    static async capture<T>(fn: () => T | Promise<T>): Promise<Computed<T>> {
        // A capture context (no computed) records the last captured Computed without adding
        // dependant edges — mirrors C# ComputeContext(CallOptions.Capture) + Computed.Capture.
        // The parent link keeps reentrancy detection working through capture — in C# lock
        // reentry flows through ExecutionContext regardless of BeginCapture.
        const currentCtx = AsyncContext.getOrCreate();
        const captureCtx = new ComputeContext(
            undefined,
            currentCtx.get(computeContextKey)
        );
        const asyncCtx = currentCtx.with(computeContextKey, captureCtx);
        try {
            await asyncCtx.run(fn);
        } catch (e) {
            if (isCancellation(e)) throw e;
            const captured = captureCtx.captured;
            if (captured?.hasError) return captured as Computed<T>;

            throw e;
        }
        const captured = captureCtx.captured;
        if (captured === undefined)
            throw new Error(
                'No Computed was captured — fn must call a compute function.'
            );
        return captured as Computed<T>;
    }

    readonly input: ComputedInput;
    readonly options: ComputedOptions;
    private _version: number;
    private _state: ConsistencyState;
    private _output: Result<T> | undefined;
    private _invalidatePending = false;
    private _errorTimer: ReturnType<typeof setTimeout> | undefined;
    private _dependencies = new Set<Computed<unknown>>();
    private _dependants = new Map<number, WeakRef<Computed<unknown>>>();
    private _onInvalidated = new EventHandlerSet<void>();
    readonly _renewer: (() => Computed<T> | Promise<Computed<T>>) | undefined;

    constructor(
        input: ComputedInput,
        renewer?: () => Computed<T> | Promise<Computed<T>>,
        options: ComputedOptions = ComputedOptions.default
    ) {
        this.input = input;
        this.options = options;
        this._renewer = renewer;
        this._version = ++_nextVersion;
        this._state = ConsistencyState.Computing;
    }

    get version(): number {
        return this._version;
    }

    get output(): Result<T> {
        if (this._output === undefined)
            throw new Error('Computed has no output yet.');
        return this._output;
    }

    get hasValue(): boolean {
        return this.output.hasValue;
    }

    get hasError(): boolean {
        return this.output.hasError;
    }

    get value(): T {
        return this.output.value;
    }

    get error(): unknown {
        return this.output.error;
    }

    get valueOrUndefined(): T | undefined {
        return this.output.valueOrUndefined;
    }

    get state(): ConsistencyState {
        return this._state;
    }

    get isConsistent(): boolean {
        return this._state === ConsistencyState.Consistent;
    }

    get isComputing(): boolean {
        return this._state === ConsistencyState.Computing;
    }

    get dependencies(): ReadonlySet<Computed<unknown>> {
        return this._dependencies;
    }

    update(): Computed<T> | Promise<Computed<T>> {
        if (this._state === ConsistencyState.Consistent) return this;
        const latest = this._latest();
        if (latest?.isConsistent) return latest;
        if (this._renewer !== undefined) {
            const renewer = this._renewer;
            // Run the renewer with the compute context cleared so renewal never
            // records a dependency edge in the ambient computation — TS analog of
            // C# Computed.UpdateUntyped's BeginIsolation.
            const isolated = (AsyncContext.current ?? AsyncContext.empty).with(
                computeContextKey,
                undefined
            );
            return isolated.run(renewer);
        }
        throw new Error(
            'Cannot recompute: Computed is invalidated and has no renewer.'
        );
    }

    protected _latest(): Computed<T> | undefined {
        return ComputedRegistry.get(this.input as string) as
            | Computed<T>
            | undefined;
    }

    use(asyncContext?: AsyncContext): T | Promise<T> {
        const ctx = ComputeContext.from(asyncContext);
        const updated = this.update();
        if (updated instanceof Promise) {
            return updated.then(c => {
                ctx?.captureDependency(c as Computed<unknown>);
                return c.value;
            });
        }
        ctx?.captureDependency(updated as Computed<unknown>);
        return updated.value;
    }

    useInconsistent(asyncContext?: AsyncContext): T {
        ComputeContext.from(asyncContext)?.captureDependency(
            this as Computed<unknown>
        );
        return this.value;
    }

    setOutput(output: Result<T> | T): void {
        if (this._state !== ConsistencyState.Computing)
            throw new Error('Cannot set output on a non-computing Computed.');
        this._output = output instanceof Result ? output : result(output);
        this._state = ConsistencyState.Consistent;
        this._register();
        if (this._invalidatePending) {
            this._invalidatePending = false;
            this.invalidate();
            return;
        }

        const delay = this.options.errorAutoInvalidateDelay;
        if (this._output.hasError && delay !== Infinity && delay > 0) {
            this._errorTimer = setTimeout(() => this.invalidate(), delay);
            (this._errorTimer as { unref?: () => void }).unref?.();
        }
    }

    protected _register(): void {
        ComputedRegistry.register(this as Computed<unknown>);
    }

    protected _unregister(): void {
        ComputedRegistry.unregister(this as Computed<unknown>);
    }

    // Never throws — mirrors .NET Computed.Invalidate: own handlers fire first
    // (each isolated), then dependants propagate unconditionally, then unregister.
    invalidate(): void {
        if (this._state === ConsistencyState.Invalidated)
            return;
        if (this._state === ConsistencyState.Computing) {
            // Defer until setOutput transitions to Consistent — matches .NET, where
            // invalidating a still-computing Computed sets a pending flag.
            this._invalidatePending = true;
            return;
        }
        this._state = ConsistencyState.Invalidated;
        if (this._errorTimer !== undefined) {
            clearTimeout(this._errorTimer);
            this._errorTimer = undefined;
        }
        try {
            this._onInvalidated.triggerSafe(undefined, e =>
                errorLog?.log('onInvalidated handler failed', e)
            );
            this._onInvalidated.clear();
        } finally {
            for (const dependency of this._dependencies)
                dependency._dependants.delete(this._version);
            this._dependencies.clear();

            for (const [, ref] of this._dependants) {
                const dependant = ref.deref();
                if (dependant != null) {
                    try {
                        dependant.invalidate();
                    } catch (e) {
                        errorLog?.log('Error while invalidating dependant', e);
                    }
                }
            }
            this._dependants.clear();

            this._unregister();
        }
    }

    onInvalidated(handler: () => void): void {
        if (this._state === ConsistencyState.Invalidated) {
            handler();
            return;
        }
        this._onInvalidated.add(handler);
    }

    whenInvalidated(abortSignal?: AbortSignal): Promise<void> {
        if (this._state === ConsistencyState.Invalidated)
            return Promise.resolve();
        if (abortSignal?.aborted)
            // eslint-disable-next-line @typescript-eslint/prefer-promise-reject-errors -- reason falls back to an Error; abortSignal.reason mirrors throwIfAborted()
            return Promise.reject(
                abortSignal.reason ?? new Error('Operation cancelled.')
            );

        const ps = new PromiseSource<void>();
        let onAbort: (() => void) | undefined;
        const onInvalidated = () => {
            if (onAbort !== undefined)
                abortSignal!.removeEventListener('abort', onAbort);
            ps.resolve(undefined);
        };
        this._onInvalidated.add(onInvalidated);
        if (abortSignal !== undefined) {
            onAbort = () => {
                this._onInvalidated.remove(onInvalidated);
                ps.reject(
                    abortSignal.reason ?? new Error('Operation cancelled.')
                );
            };
            abortSignal.addEventListener('abort', onAbort, { once: true });
        }

        return ps;
    }

    addDependency(dependency: Computed<unknown>): void {
        if (this._state !== ConsistencyState.Computing)
            return;
        if (dependency._state === ConsistencyState.Invalidated) {
            this.invalidate();
            return;
        }
        this._dependencies.add(dependency);
        dependency._dependants.set(
            this._version,
            new WeakRef(this as Computed<unknown>)
        );
    }
}

/** Computed variant for State types — skips registry registration since the State holds a direct reference. */
export class StateBoundComputed<T> extends Computed<T> {
    constructor(
        input: ComputedInput,
        renewer?: () => Computed<T> | Promise<Computed<T>>,
        options: ComputedOptions = ComputedOptions.mutableStateDefault
    ) {
        super(input, renewer, options);
    }

    protected override _latest(): Computed<T> | undefined {
        return (this.input as State<T>).computed;
    }

    protected override _register(): void {
        // No registration — state holds direct reference
    }

    protected override _unregister(): void {
        // No unregistration — was never registered
    }
}
