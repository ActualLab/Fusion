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

  get(input: ComputedInput): Computed<unknown> | undefined {
    const ref = this._entries.get(input.key);
    if (ref === undefined) return undefined;
    const computed = ref.deref();
    if (computed === undefined) {
      this._entries.delete(input.key);
      return undefined;
    }
    return computed;
  }

  register(computed: Computed<unknown>): void {
    const key = computed.input.key;
    this._entries.set(key, new WeakRef(computed));
    this._finalization.register(computed, key);
  }

  remove(input: ComputedInput): void {
    this._entries.delete(input.key);
  }

  clear(): void {
    this._entries.clear();
  }
}

// Global singleton registry
export const computedRegistry = new ComputedRegistry();
