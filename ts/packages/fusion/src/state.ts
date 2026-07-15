import {
    type AsyncContext,
    type IResult,
    PromiseSource,
    resolvedVoidPromise,
    type Result,
} from '@actuallab/core';
import { type Computed, StateBoundComputed } from './computed.js';

/** Base class for all reactive state types. */
export abstract class State<T> implements IResult<T> {
    protected _computed!: Computed<T>;
    protected _updateIndex = 0;
    protected _lastNonErrorValue: T | undefined;
    private _whenUpdatedSource: PromiseSource<void> | null = null;
    private _isDisposed = false;

    get updateIndex(): number {
        return this._updateIndex;
    }

    get isDisposed(): boolean {
        return this._isDisposed;
    }

    get lastNonErrorValue(): T | undefined {
        return this._lastNonErrorValue;
    }

    get hasValue(): boolean {
        return this._computed.hasValue;
    }

    get hasError(): boolean {
        return this._computed.hasError;
    }

    get value(): T {
        return this._computed.value;
    }

    get error(): unknown {
        return this._computed.error;
    }

    get valueOrUndefined(): T | undefined {
        return this._computed.valueOrUndefined;
    }

    get output(): Result<T> {
        return this._computed.output;
    }

    get computed(): Computed<T> {
        return this._computed;
    }

    use(asyncContext?: AsyncContext): T | Promise<T> {
        return this._computed.use(asyncContext);
    }

    useInconsistent(asyncContext?: AsyncContext): T {
        return this._computed.useInconsistent(asyncContext);
    }

    update(): Computed<T> | Promise<Computed<T>> {
        return this._computed.update();
    }

    recompute(): Computed<T> | Promise<Computed<T>> {
        this._computed.invalidate();
        return this.update();
    }

    whenInvalidated(): Promise<void> {
        return this._computed.whenInvalidated();
    }

    // Versioned wait: resolves at once when a newer generation than sinceIndex
    // already landed (no lost wakeup), rejects once disposed (C# StateSnapshot.WhenUpdated).
    whenUpdated(sinceIndex: number = this._updateIndex): Promise<void> {
        if (this._isDisposed)
            return Promise.reject(State._disposedError());
        if (this._updateIndex > sinceIndex)
            return resolvedVoidPromise;

        return (this._whenUpdatedSource ??= new PromiseSource<void>());
    }

    whenFirstTimeUpdated(): Promise<void> {
        return this.whenUpdated(0);
    }

    protected _initialize(
        output: Result<T> | T,
        renewer?: () => Computed<T> | Promise<Computed<T>>
    ): void {
        this._computed = this._createComputed(renewer);
        this._computed.setOutput(output);
        this._lastNonErrorValue = this._computed.valueOrUndefined;
    }

    protected _createComputed(
        renewer?: () => Computed<T> | Promise<Computed<T>>
    ): Computed<T> {
        return new StateBoundComputed<T>(this, renewer);
    }

    protected _update(computed: Computed<T>, output: Result<T> | T): void {
        const replaced = this._computed as Computed<T> | undefined;
        computed.setOutput(output);
        this._computed = computed;
        if (replaced?.isConsistent) replaced.invalidate();
        if (computed.hasValue) this._lastNonErrorValue = computed.value;
        this._updateIndex++;
        this._whenUpdatedSource?.resolve(undefined);
        this._whenUpdatedSource = null;
    }

    protected _onDisposed(): void {
        if (this._isDisposed)
            return;

        this._isDisposed = true;
        this._whenUpdatedSource?.reject(State._disposedError());
        this._whenUpdatedSource = null;
    }

    private static _disposedError(): Error {
        return new Error('State is disposed.');
    }
}
