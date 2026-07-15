import { PromiseSource } from './promise-source.js';

/**
 * Typed pub/sub event handler set — similar to .NET's EventHandler<T> multicast delegate.
 * Deviation: Set-backed, so adding the same handler twice fires it once (a C# delegate fires it twice).
 */
export class EventHandlerSet<T> {
    private _handlers = new Set<(arg: T) => void>();

    get count(): number {
        return this._handlers.size;
    }

    add(handler: (arg: T) => void): void {
        this._handlers.add(handler);
    }

    remove(handler: (arg: T) => void): void {
        this._handlers.delete(handler);
    }

    trigger(arg: T): void {
        // Snapshot so handlers added during dispatch aren't invoked by it (multicast-delegate parity).
        for (const handler of [...this._handlers])
            handler(arg);
    }

    clear(): void {
        this._handlers.clear();
    }

    whenNext(): Promise<T> {
        const ps = new PromiseSource<T>();
        const handler = (arg: T) => {
            this._handlers.delete(handler);
            ps.resolve(arg);
        };
        this._handlers.add(handler);
        return ps;
    }
}
