import type { Computed } from "./computed.js";
import type { ComputedInput } from "./computed-input.js";

/** WeakRef-based global cache of Computed instances — allows GC of unused computed values. */
export class ComputedRegistry {
  private _entries = new Map<string, WeakRef<Computed<unknown>>>();
  private _finalization: FinalizationRegistry<string>;

  constructor() {
    this._finalization = new FinalizationRegistry<string>((key) => {
      this._entries.delete(key);
    });
  }

  get size(): number {
    return this._entries.size;
  }

  get(key: string): Computed<unknown> | undefined {
    const ref = this._entries.get(key);
    if (ref === undefined) return undefined;
    const computed = ref.deref();
    if (computed === undefined) {
      this._entries.delete(key);
      return undefined;
    }
    return computed;
  }

  register(computed: Computed<unknown>): void {
    const key = computed.input as string;
    this._entries.set(key, new WeakRef(computed));
    this._finalization.register(computed, key);
  }

  remove(key: string): void {
    this._entries.delete(key);
  }

  clear(): void {
    this._entries.clear();
  }
}

// Global singleton registry
export const computedRegistry = new ComputedRegistry();
