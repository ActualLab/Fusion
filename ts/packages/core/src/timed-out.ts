/**
 * Sentinel returned/thrown by code that distinguishes "the operation took
 * too long" from a generic rejection. Identity-comparable via the
 * `instance` singleton — callers can branch on `result === TimedOut.instance`.
 */
export class TimedOut {
    static readonly instance: TimedOut = new TimedOut();
}
