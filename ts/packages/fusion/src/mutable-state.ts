import { type AsyncContext, Result, result } from '@actuallab/core';
import { type Computed, StateBoundComputed } from './computed.js';
import { State } from './state.js';

/** Manually-settable reactive state that can participate in the Fusion dependency graph. */
export class MutableState<T> extends State<T> {
    private _nextOutput: Result<T>;

    private _renewer = (): Computed<T> => {
        const computed = this._createComputed(this._renewer);
        this._update(computed, this._nextOutput);
        this._wireRenewal(computed);
        return computed;
    };

    constructor(initialOutput: Result<T> | T) {
        super();
        this._nextOutput =
            initialOutput instanceof Result ? initialOutput : result(initialOutput);
        this._initialize(this._nextOutput, this._renewer);
        this._wireRenewal(this._computed);
    }

    override use(asyncContext?: AsyncContext): T {
        return this._computed.use(asyncContext) as T;
    }

    set(output: Result<T> | T): void {
        const next = output instanceof Result ? output : result(output);
        if (this._nextOutput.equals(next))
            return;

        this._nextOutput = next;
        this._computed.invalidate();
    }

    // Protected/internal methods

    protected override _createComputed(
        renewer?: () => Computed<T> | Promise<Computed<T>>
    ): Computed<T> {
        return new MutableStateBoundComputed<T>(this, renewer);
    }

    // Private methods

    // Renew synchronously on invalidation so a MutableState is never observed invalidated.
    // update() de-dupes: if the computed was already renewed, it returns the latest instead.
    private _wireRenewal(computed: Computed<T>): void {
        computed.onInvalidated(() => void computed.update());
    }
}

class MutableStateBoundComputed<T> extends StateBoundComputed<T> {
    // Errors never auto-invalidate here (C# ComputedOptions.MutableStateDefault) —
    // with synchronous renewal, the default timer would loop an error-holding state forever.
    protected override get _errorAutoInvalidateDelay(): number {
        return 0;
    }
}
