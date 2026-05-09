// Initialization helpers for the logging system.
//
// The Log class uses a single Map<string, LogLevel> to determine the minimum
// level for each scope.  This module is responsible for:
//   - Persisting the map across page reloads via localStorage on the main
//     thread, and via IndexedDB for workers.
//   - Exposing a globally accessible LogLevelController so developers can tweak
//     levels at runtime from the browser dev console (globalThis.logLevels).
//
// initLogging() is idempotent and called automatically on the first Log.get().
//
// Runtime override examples (paste into browser dev console):
//   logLevels.override('rpc.RpcPeer', 1)        // exact: one scope to Debug
//   logLevels.override('rpc.*', 1)              // prefix: all rpc.* scopes
//   logLevels.override('*Video*', 1)            // contains: every scope with 'Video'
//   logLevels.override('*Decoder', 1)           // suffix: every scope ending in 'Decoder'
//   logLevels.reset()                           // reset to package defaults
//   logLevels.clear()                           // clear all overrides

import { Log, LogLevel } from './logging.js';

const GlobalThisKey = 'logLevels';
const StorageKey = 'logLevels';
const DateStorageKey = `${StorageKey}.date`;
const MaxStorageAge = 86_400_000 * 3; // 3 days
const IndexedDbName = 'actuallab-logging';
const IndexedDbStore = 'settings';
const IndexedDbKey = 'logLevels';

const localStorage: Storage | null = tryGetLocalStorage();

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

/** Worker bootstrap path: await this before dynamically importing any module
 *  that may call getLogs() at top level. */
export async function initWorkerLogging(): Promise<void> {
    Log.defaultMinLevel = LogLevel.Warn;
    const minLevels = Log.minLevels;
    const g = globalThis as Record<string, unknown>;
    const existing = g[GlobalThisKey] as LogLevelController | undefined;

    if (existing !== undefined) {
        for (const [k, v] of existing.getMinLevels())
            minLevels.set(k, v);
        return;
    }

    g[GlobalThisKey] = new LogLevelController(minLevels);
    const wasRestored = await restoreFromIndexedDb(minLevels);
    if (!wasRestored)
        reset(minLevels, false);
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

    /** Override the level for every scope matching a glob-like pattern.
     *  `*` matches any sequence of characters; everything else is literal.
     *  Examples:
     *    override('rpc.RpcPeer', 1)   // exact — one scope to Debug
     *    override('rpc.*',      1)    // prefix — every scope starting with 'rpc.'
     *    override('*rpc*',      1)    // contains — every scope with 'rpc' in it
     *    override('*Peer',      1)    // suffix — every scope ending with 'Peer'
     *  Matches scopes that are user-overridden, package-registered (defaults),
     *  or have ever been requested via Log.get().  If the pattern has no `*`
     *  and nothing known matches, the literal scope is set anyway — so an
     *  exact-name override placed before its scope is loaded still applies.
     *  Persisted to localStorage + IndexedDB. */
    public override(pattern: string, newLevel: LogLevel): void {
        const matched = new Set<string>();
        if (pattern.includes('*')) {
            const re = patternToRegExp(pattern);
            for (const [scope] of this.minLevels)
                if (re.test(scope)) matched.add(scope);
            for (const [scope] of Log.scopeDefaults)
                if (re.test(scope)) matched.add(scope);
            for (const scope of Log.knownScopes)
                if (re.test(scope)) matched.add(scope);
        }
        else {
            matched.add(pattern);
        }
        for (const scope of matched)
            this.minLevels.set(scope, newLevel);
        persist(this.minLevels);
    }

    /** Reset all overrides; package defaults take effect again.  Persisted. */
    public reset(): void {
        reset(this.minLevels);
    }

    /** Clear all overrides (no global default unless `defaultLevel` is given). */
    public clear(defaultLevel?: LogLevel): void {
        this.minLevels.clear();
        if (defaultLevel !== undefined)
            this.minLevels.set('default', defaultLevel);
        persist(this.minLevels);
    }
}

function patternToRegExp(pattern: string): RegExp {
    // Escape regex metacharacters except `*`, then turn each `*` into `.*`.
    const escaped = pattern.replace(/[.+?^${}()|[\]\\]/g, '\\$&').replace(/\*/g, '.*');
    return new RegExp(`^${escaped}$`);
}

interface PersistedLogLevels {
    date: number;
    entries: [string, LogLevel][];
}

function restore(minLevels: Map<string, LogLevel>): boolean {
    const snapshot = readFromStorage(localStorage);
    if (!snapshot)
        return false;

    return applySnapshot(minLevels, snapshot);
}

async function restoreFromIndexedDb(minLevels: Map<string, LogLevel>): Promise<boolean> {
    const snapshot = await readFromIndexedDb();
    if (!snapshot)
        return false;

    return applySnapshot(minLevels, snapshot);
}

function readFromStorage(storage: Storage | null): PersistedLogLevels | null {
    if (!storage)
        return null;

    try {
        const dateJson = storage.getItem(DateStorageKey);
        const entriesJson = storage.getItem(StorageKey);
        if (!dateJson || !entriesJson)
            return null;

        return {
            date: JSON.parse(dateJson) as number,
            entries: JSON.parse(entriesJson) as [string, LogLevel][],
        };
    } catch {
        return null;
    }
}

function applySnapshot(minLevels: Map<string, LogLevel>, snapshot: PersistedLogLevels): boolean {
    if (typeof snapshot.date !== 'number')
        return false;
    if (Date.now() - snapshot.date > MaxStorageAge)
        return false;
    if (!Array.isArray(snapshot.entries))
        return false;

    minLevels.clear();
    for (const entry of snapshot.entries) {
        // Runtime guard for JSON-parsed input — types claim it's a tuple, but
        // localStorage may carry corrupted data. eslint flags the length check
        // as always-false against the static type; that's the point.
        // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
        if (!Array.isArray(entry) || entry.length !== 2)
            return false;
        const [key, value] = entry;
        if (typeof key !== 'string' || typeof value !== 'number')
            return false;
        minLevels.set(key, value);
    }

    return true;
}

function persist(minLevels: Map<string, LogLevel>): boolean {
    const snapshot = createSnapshot(minLevels);
    let wasPersisted = false;

    if (localStorage) {
        try {
            localStorage.setItem(DateStorageKey, JSON.stringify(snapshot.date));
            localStorage.setItem(StorageKey, JSON.stringify(snapshot.entries));
            wasPersisted = true;
        } catch {
            // Storage may be unavailable in private / sandboxed contexts.
        }
    }

    void writeToIndexedDb(snapshot);
    return wasPersisted;
}

function createSnapshot(minLevels: Map<string, LogLevel>): PersistedLogLevels {
    return {
        date: Date.now(),
        entries: Array.from(minLevels.entries()),
    };
}

function tryGetLocalStorage(): Storage | null {
    if (typeof globalThis === 'undefined' || !('localStorage' in globalThis))
        return null;

    try {
        return (globalThis as unknown as { localStorage: Storage }).localStorage;
    } catch {
        return null;
    }
}

async function openLoggingDb(): Promise<IDBDatabase | null> {
    if (typeof indexedDB === 'undefined')
        return null;

    return new Promise(resolve => {
        const request = indexedDB.open(IndexedDbName, 1);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(IndexedDbStore))
                db.createObjectStore(IndexedDbStore);
        };
        request.onerror = () => resolve(null);
        request.onblocked = () => resolve(null);
        request.onsuccess = () => resolve(request.result);
    });
}

async function readFromIndexedDb(): Promise<PersistedLogLevels | null> {
    const db = await openLoggingDb();
    if (!db)
        return null;

    return new Promise(resolve => {
        const tx = db.transaction(IndexedDbStore, 'readonly');
        const request = tx.objectStore(IndexedDbStore).get(IndexedDbKey);
        request.onerror = () => resolve(null);
        request.onsuccess = () => resolve(request.result as PersistedLogLevels | null);
        tx.oncomplete = () => db.close();
        tx.onerror = () => db.close();
        tx.onabort = () => db.close();
    });
}

async function writeToIndexedDb(snapshot: PersistedLogLevels): Promise<void> {
    const db = await openLoggingDb();
    if (!db)
        return;

    await new Promise<void>(resolve => {
        const tx = db.transaction(IndexedDbStore, 'readwrite');
        tx.objectStore(IndexedDbStore).put(snapshot, IndexedDbKey);
        tx.oncomplete = () => {
            db.close();
            resolve();
        };
        tx.onerror = () => {
            db.close();
            resolve();
        };
        tx.onabort = () => {
            db.close();
            resolve();
        };
    });
}

function reset(minLevels: Map<string, LogLevel>, mustPersist = true): void {
    minLevels.clear();
    // Add per-scope defaults here for development.
    // Use prefix conventions: 'rpc.', 'fusion.', 'app.', etc.

    // minLevels.set('rpc.RpcPeer', LogLevel.Debug);
    // minLevels.set('fusion.Computed', LogLevel.Debug);

    if (mustPersist)
        persist(minLevels);
}
