import type { Disposable } from "./disposable.js";

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
export const abortSignalKey =
  new AsyncContextKey<AbortSignal | undefined>("AbortSignal", undefined);

export class AsyncContext {
  static current: AsyncContext | undefined = undefined;

  /** Immutable empty singleton — avoids allocating new AsyncContext(). */
  static readonly empty: AsyncContext = new AsyncContext();

  private static _defaults = new Map<symbol, unknown>();

  private _values: Map<symbol, unknown>;

  constructor(values?: Map<symbol, unknown>) {
    this._values = values ?? new Map();
  }

  get<T>(key: AsyncContextKey<T>): T {
    if (this._values.has(key.id)) return this._values.get(key.id) as T;
    if (AsyncContext._defaults.has(key.id)) return AsyncContext._defaults.get(key.id) as T;
    return key.defaultValue;
  }

  with<T>(key: AsyncContextKey<T>, value: T): AsyncContext {
    const copy = new Map(this._values);
    copy.set(key.id, value);
    return new AsyncContext(copy);
  }

  activate(): Disposable {
    const prev = AsyncContext.current;
    AsyncContext.current = this;
    return { dispose: () => { AsyncContext.current = prev; } };
  }

  run<R>(fn: () => R): R {
    const prev = AsyncContext.current;
    AsyncContext.current = this;
    try {
      return fn();
    } finally {
      AsyncContext.current = prev;
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
}
