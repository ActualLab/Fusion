import {
    AsyncContext,
    PromiseSource,
    type Result,
    result,
    errorResult,
} from '@actuallab/core';
import { type Computed, StateBoundComputed } from './computed.js';
import { ComputeContext, computeContextKey } from './compute-context.js';
import { getLogs } from './logging.js';
import { defaultUpdateDelayer, type UpdateDelayer } from './update-delayer.js';
import { State } from './state.js';

const { errorLog } = getLogs('ComputedState');

export type StateComputer<T> = () => T | Promise<T>;

/** Constructor options for ComputedState. */
export interface ComputedStateOptions<T> {
    initialValue?: T;
    initialOutput?: Result<T>;
    updateDelayer?: UpdateDelayer;
}

/** Auto-updating reactive state wrapper — re-computes on invalidation with configurable delay. */
export class ComputedState<T> extends State<T> {
    private _computer: StateComputer<T>;
    private _updateDelayer: UpdateDelayer;
    private _disposeController: AbortController;
    private _cancelDelaySource = new PromiseSource<void>();
    private _renewer: () => Computed<T> | Promise<Computed<T>>;

    constructor(computer: StateComputer<T>, options?: ComputedStateOptions<T>) {
        super();
        this._computer = computer;
        this._updateDelayer = options?.updateDelayer ?? defaultUpdateDelayer;
        this._disposeController = new AbortController();
        this._renewer = async () => {
            const sinceIndex = this._updateIndex;
            this._cancelDelaySource.resolve(undefined);
            await this.whenUpdated(sinceIndex);
            return this._computed;
        };

        // The initial computed is created pre-invalidated (C# State.cs:246) so inherited
        // members (use/update/recompute/whenInvalidated) await the first real computation
        // instead of returning the placeholder, and dependants never latch onto it.
        this._initialize(
            options?.initialOutput ?? (options?.initialValue as T),
            this._renewer
        );
        this._computed.invalidate();

        void this._updateCycle();
    }

    dispose(): void {
        if (this.isDisposed)
            return;

        this._onDisposed();
        this._disposeController.abort();
    }

    private async _updateCycle(): Promise<void> {
        const disposeSignal = this._disposeController.signal;
        let retryCount = 0;
        try {
            while (!disposeSignal.aborted) {
                // Compute
                const computed = new StateBoundComputed<T>(this, this._renewer);
                const computeCtx = new ComputeContext(
                    computed as StateBoundComputed<unknown>
                );
                const asyncCtx = (
                    AsyncContext.current ?? AsyncContext.empty
                ).with(computeContextKey, computeCtx);

                let output: Result<T>;
                try {
                    const value = asyncCtx.run(() => this._computer());
                    const resolved =
                        value instanceof Promise ? await value : value;
                    output = result(resolved);
                } catch (e) {
                    output = errorResult(e);
                }
                if (this.isDisposed)
                    return; // Disposed mid-computation — publish nothing, terminate the cycle.

                this._update(computed, output);
                retryCount = output.hasError ? retryCount + 1 : 0;
                if (this._cancelDelaySource.isCompleted)
                    this._cancelDelaySource = new PromiseSource<void>();

                // Wait for invalidation (or cancellation via dispose)
                try {
                    await computed.whenInvalidated(disposeSignal);
                } catch {
                    return; // Cancelled via dispose
                }

                // Wait for delay (cancellable by renewer); retryCount grows the backoff (S8).
                await Promise.race([
                    this._updateDelayer(retryCount, disposeSignal),
                    this._cancelDelaySource,
                ]);
            }
        } catch (e) {
            // Mirrors ComputedState.cs:188 — UpdateCycle failed and stopped.
            if (!disposeSignal.aborted)
                errorLog?.log('UpdateCycle() failed and stopped', e);
        }
    }
}
