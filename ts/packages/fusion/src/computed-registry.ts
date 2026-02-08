import type { Computed } from "./computed.js";

/** WeakRef-based global cache of Computed instances — allows GC of unused computed values. */
export class ComputedRegistry {
  private static _entries = new Map<string, WeakRef<Computed<unknown>>>();
  private static _finalization = new FinalizationRegistry<string>((key) => {
    ComputedRegistry._entries.delete(key);
  });

  static get size(): number {
    return ComputedRegistry._entries.size;
  }

  static get(key: string): Computed<unknown> | undefined {
    const ref = ComputedRegistry._entries.get(key);
    if (ref === undefined) return undefined;
    const computed = ref.deref();
    if (computed === undefined) {
      ComputedRegistry._entries.delete(key);
      return undefined;
    }
    return computed;
  }

  static register(computed: Computed<unknown>): void {
    const key = computed.input as string;
    ComputedRegistry._entries.set(key, new WeakRef(computed));
    ComputedRegistry._finalization.register(computed, key, computed);
  }

  static unregister(computed: Computed<unknown>): void {
    const key = computed.input as string;
    ComputedRegistry._entries.delete(key);
    ComputedRegistry._finalization.unregister(computed);
  }
}
