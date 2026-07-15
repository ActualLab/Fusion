import { PromiseSource } from './promise-source.js';

// Auto-reset, edge-triggered async wakeup. One notify() releases every waiter
// parked at that moment, then arms a fresh promise for subsequent waits — a
// notify with no waiter is NOT latched (pure edge trigger).
//
// Lost-wakeup-safe usage: take the wait promise BEFORE checking the condition,
// so a notify() that lands between the check and the await still wins:
//
//   const w = signal.wait();
//   if (conditionAlreadyMet)
//       consume();                          // notify before wait() already set the condition
//   else
//       await Promise.race([w, abortWait]); // notify after wait() resolves w
export class AsyncSignal {
    private source = new PromiseSource<void>();

    wait(): Promise<void> {
        return this.source.promise;
    }

    notify(): void {
        const source = this.source;
        this.source = new PromiseSource<void>();
        source.resolve();
    }
}
