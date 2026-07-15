import type { Disposable } from './disposable.js';

/** Typed key for values stored in AsyncContext — each subsystem defines its own keys. */
export class AsyncContextKey<T> {
    readonly id: symbol;
    readonly defaultValue: T;

    constructor(name: string, defaultValue: T) {
        this.id = Symbol(name);
        this.defaultValue = defaultValue;
    }
}

/** General-purpose typed context container — named for forward-compatibility with TC39 AsyncContext proposal. */
export const abortSignalKey = new AsyncContextKey<AbortSignal | undefined>(
    'AbortSignal',
    undefined
);

interface AsyncLocalStorageLike {
    getStore(): AsyncContext | undefined;
    run<R>(store: AsyncContext, fn: () => R): R;
    enterWith(store: AsyncContext | undefined): void;
}

// Node's AsyncLocalStorage, if available. When active it backs AsyncContext.current, so the
// current context flows across awaits exactly like C#'s AsyncLocal ComputeContext — dependencies
// used after the first await are still captured. In browsers (no node:async_hooks) it stays
// undefined and AsyncContext falls back to the synchronous-prefix-only static field; there,
// dependencies used after the first await are captured only through the ctx argument threaded
// into a compute body (see ComputeFunction.invoke's trailing AsyncContext argument).
const _alsImpl: AsyncLocalStorageLike | undefined = detectAsyncLocalStorage();
let _als: AsyncLocalStorageLike | undefined = _alsImpl;

function detectAsyncLocalStorage(): AsyncLocalStorageLike | undefined {
    // process.getBuiltinModule (Node >= 20.16) is the only synchronous, bundler-safe way to
    // load node:async_hooks here: a static import breaks browsers, and a top-level-await
    // dynamic import breaks the CJS build output.
    const proc = (
        globalThis as {
            process?: { getBuiltinModule?: (id: string) => unknown };
        }
    ).process;
    const asyncHooks = proc?.getBuiltinModule?.('node:async_hooks') as
        | { AsyncLocalStorage: new () => AsyncLocalStorageLike }
        | undefined;
    return asyncHooks === undefined
        ? undefined
        : new asyncHooks.AsyncLocalStorage();
}

export class AsyncContext {
    /** Immutable empty singleton — avoids allocating new AsyncContext(). */
    static readonly empty: AsyncContext = new AsyncContext();

    private static _current: AsyncContext | undefined = undefined;
    private static _defaults = new Map<symbol, unknown>();

    private _values: Map<symbol, unknown>;

    constructor(values?: Map<symbol, unknown>) {
        this._values = values ?? new Map<symbol, unknown>();
    }

    static get current(): AsyncContext | undefined {
        return _als?.getStore() ?? AsyncContext._current;
    }

    static set current(value: AsyncContext | undefined) {
        if (_als !== undefined)
            _als.enterWith(value);
        else
            AsyncContext._current = value;
    }

    /** True when AsyncContext.current flows across awaits (Node AsyncLocalStorage is active). */
    static get isAsyncLocalStorageActive(): boolean {
        return _als !== undefined;
    }

    get<T>(key: AsyncContextKey<T>): T {
        if (this._values.has(key.id)) return this._values.get(key.id) as T;
        if (AsyncContext._defaults.has(key.id))
            return AsyncContext._defaults.get(key.id) as T;
        return key.defaultValue;
    }

    with<T>(key: AsyncContextKey<T>, value: T): AsyncContext {
        const copy = new Map(this._values);
        copy.set(key.id, value);
        return new AsyncContext(copy);
    }

    activate(): Disposable {
        const prev = AsyncContext.current;
        const als = _als;
        if (als !== undefined) {
            // enterWith persists for the rest of this async branch, so a leaked (undisposed)
            // activate() can only affect its own branch — the compute pipeline always uses run(),
            // which is properly scoped, so dependency capture is never attributed to a leaked ctx.
            als.enterWith(this);
            return { dispose: () => als.enterWith(prev) };
        }
        AsyncContext._current = this;
        return {
            dispose: () => {
                AsyncContext._current = prev;
            },
        };
    }

    run<R>(fn: () => R): R {
        if (_als !== undefined)
            return _als.run(this, fn);
        const prev = AsyncContext._current;
        AsyncContext._current = this;
        try {
            return fn();
        } finally {
            AsyncContext._current = prev;
        }
    }

    /** Strip THIS exact instance from args if it's the last element (reference equality). */
    stripFromArgs(args: unknown[]): unknown[] {
        return args[args.length - 1] === this ? args.slice(0, -1) : args;
    }

    /** Resolve: return ctx if provided, otherwise AsyncContext.current. */
    static from(ctx: AsyncContext | undefined): AsyncContext | undefined {
        return ctx ?? AsyncContext.current;
    }

    /** Extract AsyncContext from last arg if instanceof AsyncContext, else fall back to .current. */
    static fromArgs(args: unknown[]): AsyncContext | undefined {
        const last = args[args.length - 1];
        return last instanceof AsyncContext ? last : AsyncContext.current;
    }

    static setDefault<T>(key: AsyncContextKey<T>, value: T): void {
        AsyncContext._defaults.set(key.id, value);
    }

    static getOrCreate(): AsyncContext {
        return AsyncContext.current ?? AsyncContext.empty;
    }

    // Toggles AsyncLocalStorage backing — for tests that exercise the browser fallback path.
    // Clears the fallback static field so no stale context bleeds across the toggle.
    static _setAsyncLocalStorageActive(active: boolean): void {
        _als = active ? _alsImpl : undefined;
        AsyncContext._current = undefined;
    }
}
