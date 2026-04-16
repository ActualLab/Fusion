// Initialization helpers for the logging system.
//
// The Log class uses a single Map<string, LogLevel> to determine the minimum
// level for each scope.  This module is responsible for:
//   - Persisting the map across page reloads via sessionStorage.
//   - Exposing a globally accessible LogLevelController so developers can tweak
//     levels at runtime from the browser dev console (globalThis.logLevels).
//
// initLogging() is idempotent and called automatically on the first Log.get().
//
// Runtime override examples (paste into browser dev console):
//   logLevels.override('rpc.RpcPeer', 1)        // set Debug for one scope
//   logLevels.overrideAll('rpc.', 1)            // set Debug for all rpc.* scopes
//   logLevels.overrideAll('fusion.', 2)         // set Info for all fusion.* scopes
//   logLevels.reset()                           // reset to package defaults
//   logLevels.clear()                           // clear all overrides

import { Log, LogLevel } from './logging.js';

const GlobalThisKey = 'logLevels';
const StorageKey = 'logLevels';
const DateStorageKey = `${StorageKey}.date`;
const MaxStorageAge = 86_400_000 * 3; // 3 days

const sessionStorage: Storage | null = (typeof globalThis !== 'undefined' && 'sessionStorage' in globalThis)
    ? (globalThis as { sessionStorage: Storage }).sessionStorage
    : null;

// Workers, worklets, and service workers run in separate JS realms with
// their own globalThis.  Within one realm, esbuild can also produce multiple
// chunks that each carry their own copy of @actuallab/core (so multiple
// distinct `Log` classes exist).  We want the install + restore + console
// message to happen exactly once per realm — gate via globalThis.logLevels.
const isMainThread = typeof window !== 'undefined';

export function initLogging(): void {
    // Warn is the global baseline — packages set per-scope defaults
    // (typically Info or Warn) via createLogProvider.
    Log.defaultMinLevel = LogLevel.Warn;
    const minLevels = Log.minLevels;
    const g = globalThis as Record<string, unknown>;
    const existing = g[GlobalThisKey] as LogLevelController | undefined;

    if (existing !== undefined) {
        // Another chunk in this realm already initialized.  Mirror its
        // overrides into our local Log.minLevels so this Log class observes
        // the same effective levels.  (Each chunk has its own Log class with
        // its own static minLevels Map — they only converge through this
        // one-shot copy at init time.)
        for (const [k, v] of existing.getMinLevels())
            minLevels.set(k, v);
        return;
    }

    g[GlobalThisKey] = new LogLevelController(minLevels);
    const wasRestored = restore(minLevels);
    if (wasRestored) {
        if (isMainThread)
            console.log('Logging: logLevels are restored');
    }
    else {
        if (isMainThread)
            console.log('Logging: logLevels are reset');
        reset(minLevels);
    }
}

export class LogLevelController {
    constructor(private minLevels: Map<string, LogLevel>)
    { }

    /** Internal: used by initLogging() in secondary bundles to share the
     *  primary bundle's minLevels map. */
    public getMinLevels(): Map<string, LogLevel> {
        return this.minLevels;
    }

    /** List the current set of overrides. */
    public list(): Record<string, LogLevel> {
        return Object.fromEntries(this.minLevels);
    }

    /** Print every known scope's effective level to the console as a table.
     *  Shows: scope, effective level, the source ('override' / 'default-key' /
     *  'package-default' / 'baseline'), and the package default for reference.
     *  Use this from the dev console to discover what to pass to override(). */
    public dump(): void {
        const baseline = Log.defaultMinLevel;
        const globalOverride = this.minLevels.get('default');
        const allScopes = new Set<string>([
            ...Log.knownScopes,
            ...Log.scopeDefaults.keys(),
            ...this.minLevels.keys(),
        ]);
        // Drop the special 'default' bucket — surfaced separately below.
        allScopes.delete('default');
        const rows: Record<string, {
            level: string;
            source: string;
            packageDefault: string;
        }> = {};
        const sorted = [...allScopes].sort();
        for (const scope of sorted) {
            const override = this.minLevels.get(scope);
            const pkgDefault = Log.scopeDefaults.get(scope);
            let level: LogLevel;
            let source: string;
            if (override !== undefined) {
                level = override;
                source = 'override';
            }
            else if (globalOverride !== undefined) {
                level = globalOverride;
                source = "minLevels.get('default')";
            }
            else if (pkgDefault !== undefined) {
                level = pkgDefault;
                source = 'package-default';
            }
            else {
                level = baseline;
                source = 'baseline';
            }
            rows[scope] = {
                level: LogLevel[level],
                source,
                packageDefault: pkgDefault === undefined ? '-' : LogLevel[pkgDefault],
            };
        }
        console.table(rows);
        console.log(
            `Baseline: ${LogLevel[baseline]}` +
            (globalOverride === undefined ? '' : `, default override: ${LogLevel[globalOverride]}`),
        );
    }

    /** Override the level for a single scope.  Persisted to sessionStorage. */
    public override(scope: string, newLevel: LogLevel): void {
        this.minLevels.set(scope, newLevel);
        persist(this.minLevels);
    }

    /** Override the level for every scope whose name starts with scopePrefix
     *  (e.g. 'rpc.' to enable verbose RPC logging).  Persisted. */
    public overrideAll(scopePrefix: string, newLevel: LogLevel): void {
        // Capture all currently-known scopes (both user overrides and package
        // defaults) that match the prefix, then apply.  Package defaults live
        // in Log.scopeDefaults; this method also accounts for them so calling
        // overrideAll('rpc.', Debug) flips every scope the rpc package
        // registered, not only the ones already overridden.
        const matched = new Set<string>();
        for (const [scope] of this.minLevels)
            if (scope.startsWith(scopePrefix)) matched.add(scope);
        for (const [scope] of Log.scopeDefaults)
            if (scope.startsWith(scopePrefix)) matched.add(scope);
        for (const scope of matched)
            this.minLevels.set(scope, newLevel);
        persist(this.minLevels);
    }

    /** Reset all overrides; package defaults take effect again.  Persisted. */
    public reset(): void {
        reset(this.minLevels);
        persist(this.minLevels);
    }

    /** Clear all overrides (no global default unless `defaultLevel` is given). */
    public clear(defaultLevel?: LogLevel): void {
        this.minLevels.clear();
        if (defaultLevel !== undefined)
            this.minLevels.set('default', defaultLevel);
        persist(this.minLevels);
    }
}

function restore(minLevels: Map<string, LogLevel>): boolean {
    if (!sessionStorage)
        return false;

    const dateJson = sessionStorage.getItem(DateStorageKey);
    if (!dateJson)
        return false;
    if (Date.now() - (JSON.parse(dateJson) as number) > MaxStorageAge)
        return false;

    const readJson = sessionStorage.getItem(StorageKey);
    if (!readJson)
        return false;

    const readMinLevels = new Map(JSON.parse(readJson) as [string, LogLevel][]);
    if (typeof readMinLevels.size !== 'number')
        return false;

    minLevels.clear();
    readMinLevels.forEach((value, key) => minLevels.set(key, value));
    return true;
}

function persist(minLevels: Map<string, LogLevel>): boolean {
    if (!sessionStorage)
        return false;

    sessionStorage.setItem(DateStorageKey, JSON.stringify(Date.now()));
    sessionStorage.setItem(StorageKey, JSON.stringify(Array.from(minLevels.entries())));
    return true;
}

function reset(minLevels: Map<string, LogLevel>): void {
    minLevels.clear();
    // Add per-scope defaults here for development.
    // Use prefix conventions: 'rpc.', 'fusion.', 'app.', etc.

    // minLevels.set('rpc.RpcPeer', LogLevel.Debug);
    // minLevels.set('fusion.Computed', LogLevel.Debug);

    persist(minLevels);
}
