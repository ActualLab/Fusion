import { EventHandlerSet } from '@actuallab/core';
import { getLogs } from './logging.js';

const { errorLog } = getLogs('UIActionTracker');

/** Singleton tracking active UI commands — mirrors .NET's UIActionTracker. */
export class UIActionTracker {
    private _activeCount = 0;
    private _lastResultAt = 0;
    private _errorTimes: number[] = [];
    readonly changed = new EventHandlerSet<void>();
    readonly errors: unknown[] = [];

    // C# UIActionTracker.Options.InstantUpdatePeriod / UIActionFailureTracker MaxDuplicateRecency.
    instantUpdatePeriod = 300;
    maxDuplicateRecency = 1000;
    maxErrors = 100;

    get isActive(): boolean {
        return this._activeCount > 0;
    }

    // C# UIActionTracker.AreInstantUpdatesEnabled — active, or within instantUpdatePeriod of the last result.
    areInstantUpdatesEnabled(): boolean {
        if (this._activeCount > 0)
            return true;
        if (this._lastResultAt === 0)
            return false;

        return this._lastResultAt + this.instantUpdatePeriod >= Date.now();
    }

    /** Run a command silently — errors are added to the error list but not thrown. */
    async run(fn: () => Promise<unknown>): Promise<void> {
        this._activeCount++;
        this.changed.trigger();
        try {
            await fn();
        } catch (e) {
            // Mirrors UIActionTracker.cs:58 — UI action failure.
            errorLog?.log('UI action failed', e);
            this._addError(e);
        } finally {
            this._onCompleted();
        }
    }

    /** Call a command and return the result — errors are added to the error list AND thrown. */
    async call<T>(fn: () => Promise<T>): Promise<T> {
        this._activeCount++;
        this.changed.trigger();
        try {
            return await fn();
        } catch (e) {
            // Mirrors UIActionTracker.cs:58 — UI action failure.
            errorLog?.log('UI action failed', e);
            this._addError(e);
            throw e;
        } finally {
            this._onCompleted();
        }
    }

    dismissError(index: number): void {
        if (index >= 0 && index < this.errors.length) {
            this.errors.splice(index, 1);
            this._errorTimes.splice(index, 1);
            this.changed.trigger();
        }
    }

    // Private methods

    private _onCompleted(): void {
        this._activeCount--;
        this._lastResultAt = Date.now();
        this.changed.trigger();
    }

    // C# UIActionFailureTracker.TryAddFailure — drop a same-name/same-message error seen within
    // maxDuplicateRecency; a size cap is the backstop against unbounded growth.
    private _addError(e: unknown): void {
        const now = Date.now();
        const { name, message } = errorInfo(e);
        const minAt = now - this.maxDuplicateRecency;
        for (let i = 0; i < this.errors.length; i++) {
            if (this._errorTimes[i] < minAt)
                continue;

            const prev = errorInfo(this.errors[i]);
            if (prev.name === name && prev.message === message)
                return;
        }

        this.errors.push(e);
        this._errorTimes.push(now);
        while (this.errors.length > this.maxErrors) {
            this.errors.shift();
            this._errorTimes.shift();
        }
        this.changed.trigger();
    }
}

function errorInfo(e: unknown): { name: string; message: string } {
    if (e instanceof Error)
        return { name: e.name, message: e.message };

    return { name: '', message: String(e) };
}

export const uiActions = new UIActionTracker();
