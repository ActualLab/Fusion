import { type Computed, ConsistencyState } from './computed.js';

/** Identifies the exact registry entry a finalizer callback is allowed to remove. */
interface FinalizationToken {
    key: string;
    ref: WeakRef<Computed<unknown>>;
}

/** WeakRef-based global cache of Computed instances — allows GC of unused computed values. */
export class ComputedRegistry {
    private static _entries = new Map<string, WeakRef<Computed<unknown>>>();
    private static _finalization = new FinalizationRegistry<FinalizationToken>(token => {
        ComputedRegistry._onFinalized(token);
    });

    static get size(): number {
        return ComputedRegistry._entries.size;
    }

    static get(key: string): Computed<unknown> | undefined {
        const ref = ComputedRegistry._entries.get(key);
        if (ref === undefined) return undefined;
        const computed = ref.deref();
        if (computed === undefined) {
            ComputedRegistry._removeEntry(key, ref);
            return undefined;
        }
        return computed;
    }

    static register(computed: Computed<unknown>): void {
        const key = computed.input as string;
        const displaced = ComputedRegistry._entries.get(key)?.deref();
        // Already registered — setOutput re-registers the instance registered at creation
        if (displaced === computed)
            return;

        if (displaced !== undefined && displaced.state !== ConsistencyState.Invalidated)
            displaced.invalidate();
        const ref = new WeakRef(computed);
        ComputedRegistry._entries.set(key, ref);
        ComputedRegistry._finalization.register(computed, { key, ref }, computed);
    }

    static unregister(computed: Computed<unknown>): void {
        const key = computed.input as string;
        if (ComputedRegistry._entries.get(key)?.deref() === computed)
            ComputedRegistry._entries.delete(key);
        ComputedRegistry._finalization.unregister(computed);
    }

    // Private methods

    private static _onFinalized(token: FinalizationToken): void {
        ComputedRegistry._removeEntry(token.key, token.ref);
    }

    // Removing by key alone would drop a live successor registered under the same key
    // while this key's predecessor was already collected but not yet finalized (I13).
    private static _removeEntry(key: string, ref: WeakRef<Computed<unknown>>): void {
        if (ComputedRegistry._entries.get(key) === ref)
            ComputedRegistry._entries.delete(key);
    }
}
