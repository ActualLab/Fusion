import { PromiseSource } from './promise-source.js';

/** Typed pub/sub event handler set — similar to .NET's EventHandler<T> multicast delegate. */
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
        for (const handler of this._handlers) handler(arg);
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
        return ps.promise;
    }
}
