// Lightweight logging primitives shared by all ActualLab TypeScript packages.
//
// Design:
//   - Log.get(scope) returns a bag of optional loggers ({ debugLog, infoLog,
//     warnLog, errorLog }).  Each is null when its level is below the scope's
//     minimum level, so the typical call site looks like:
//
//         debugLog?.log('something happened', value);
//
//     The optional chaining short-circuits before evaluating the arguments —
//     so the cost of disabled logs is just one nullish check.
//
//   - Scopes are plain strings.  Each package defines its own typed scope union
//     and uses createLogProvider<TScope>(prefix) to get a typed helper that keeps
//     scope names short at call sites while ensuring globally unique keys
//     ('rpc.RpcPeer', 'fusion.Computed', etc.).
//
//   - Minimum levels live in Log.minLevels (a Map<string, LogLevel>).  An
//     entry 'default' acts as a fallback before Log.defaultMinLevel.
//
//   - initLogging() is invoked lazily on the first Log.get() call.  It restores
//     levels from sessionStorage and installs globalThis.logLevels so devs can
//     tweak levels at runtime.

import { initLogging } from './logging-init.js';

export enum LogLevel {
    Debug = 1,
    Info,
    Warn,
    Error,
    None = 1000,
}

export interface LogRef {
    target: unknown;
    id: number;
}

interface LogRefSetItem {
    ref: LogRef;
    touchedAt: number;
}

interface LogRefData {
    __logRefId?: number;
}

class LogRefSet {
    private items: LogRefSetItem[] = [];
    private idSeed = 0;

    constructor(private capacity: number)
    { }

    public ref(data: object): LogRef {
        const itemIndex = this.items.findIndex(el => el.ref.target === data);
        if (itemIndex >= 0) {
            const existing = this.items[itemIndex];
            existing.touchedAt = Date.now();
            return existing.ref;
        }
        const id = (data as LogRefData).__logRefId ?? this.idSeed++;
        const newRef: LogRef = { target: data, id };
        (data as LogRefData).__logRefId = id;
        if (this.items.length >= this.capacity)
            this.removeOldest();
        this.items.push({ ref: newRef, touchedAt: Date.now() });
        return newRef;
    }

    private removeOldest(): void {
        let indexToEvict = 0;
        let itemToEvict = this.items[0];
        for (let i = 1; i < this.items.length; i++) {
            const item = this.items[i];
            if (item.touchedAt < itemToEvict.touchedAt) {
                itemToEvict = item;
                indexToEvict = i;
            }
        }
        this.items.splice(indexToEvict, 1);
        // Replace the live target with its string form so we don't keep the
        // object alive purely for log identity reasons.
        const ref = itemToEvict.ref;
        ref.target = ref.target?.toString();
    }
}

export class Log {
    private static isInitialized = false;
    private static logRefs = new LogRefSet(10);
    /** User-set overrides (persisted to sessionStorage). Empty by default. */
    public static readonly minLevels = new Map<string, LogLevel>();
    /** Per-scope defaults registered by packages via createLogProvider(). */
    public static readonly scopeDefaults = new Map<string, LogLevel>();
    /** Every scope ever requested via Log.get().  Used by logLevels.dump()
     *  so devs can discover what's available to override at runtime. */
    public static readonly knownScopes = new Set<string>();
    /** Global baseline for any scope without a default or override. */
    public static defaultMinLevel = LogLevel.Warn;
    public static loggerFactory: (scope: string, level: LogLevel) => Log = (scope, level) => new Log(scope, level);

    public log: (...data: unknown[]) => void;
    public trace: (...data: unknown[]) => void;

    constructor(
        public readonly scope: string,
        public readonly level: LogLevel,
    ) {
        const prefix = `[${scope}]`;
        switch (level) {
        case LogLevel.Debug:
            this.log = (...data: unknown[]) => console.debug(prefix, ...data);
            break;
        case LogLevel.Info:
            this.log = (...data: unknown[]) => console.log(prefix, ...data);
            break;
        case LogLevel.Warn:
            this.log = (...data: unknown[]) => console.warn(prefix, ...data);
            break;
        case LogLevel.Error:
            this.log = (...data: unknown[]) => console.error(prefix, ...data);
            break;
        case LogLevel.None:
            throw new Error('LogLevel.None cannot be used here');
        }
        this.trace = (...data: unknown[]) => console.trace(prefix, ...data);
    }

    public static get(scope: string): LogScopeFns {
        if (!this.isInitialized) {
            this.isInitialized = true;
            initLogging();
        }
        this.knownScopes.add(scope);

        const minLevels = this.minLevels;
        // Resolution order:
        //   1. User override for this scope (sessionStorage-persisted)
        //   2. User-set global override (minLevels.get('default'))
        //   3. Package default for this scope (registered via createLogProvider)
        //   4. Framework baseline (Log.defaultMinLevel, Warn)
        const minLevel = minLevels.get(scope)
            ?? minLevels.get('default')
            ?? this.scopeDefaults.get(scope)
            ?? this.defaultMinLevel;

        const getLogger = (level: LogLevel): Log | null =>
            level >= minLevel ? this.loggerFactory(scope, level) : null;

        return {
            logScope: scope,
            debugLog: getLogger(LogLevel.Debug),
            infoLog: getLogger(LogLevel.Info),
            warnLog: getLogger(LogLevel.Warn),
            errorLog: getLogger(LogLevel.Error),
        };
    }

    public static ref(data: object | null | undefined): object | null | undefined {
        if (!data)
            return data;
        return this.logRefs.ref(data);
    }

    public assert(predicate?: boolean, ...data: unknown[]): void {
        if (!predicate)
            this.log(...data);
    }
}

export interface LogScopeFns {
    logScope: string;
    debugLog: Log | null;
    infoLog: Log | null;
    warnLog: Log | null;
    errorLog: Log | null;
}

// Per-package factory: creates a typed getLogs(scope) that prepends the package
// prefix to the scope name before delegating to Log.get.  Keeps call sites
// concise while ensuring globally unique scope keys.
//
// `defaults` is required (not Partial) so that every scope in TScope has an
// explicit default level — TypeScript will refuse to compile if a scope is
// missing.  Defaults register into Log.scopeDefaults and act as the fallback
// when no user override is set.
//
// All declared scopes are also pre-registered in Log.knownScopes so that
// logLevels.dump() (in dev console) can list them even before they're first
// touched at runtime.
export function createLogProvider<TScope extends string>(
    prefix: string,
    defaults: Record<TScope, LogLevel>,
): (scope: TScope) => LogScopeFns {
    for (const [scope, level] of Object.entries(defaults) as [TScope, LogLevel][]) {
        const fullName = prefix + scope;
        Log.scopeDefaults.set(fullName, level);
        Log.knownScopes.add(fullName);
    }
    return scope => Log.get(prefix + scope);
}
