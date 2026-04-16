import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createLogProvider, Log, LogLevel } from '../src/index.js';

describe('Log', () => {
    beforeEach(() => {
        Log.minLevels.clear();
        Log.scopeDefaults.clear();
        Log.knownScopes.clear();
        Log.defaultMinLevel = LogLevel.Warn;
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('returns null loggers for levels below the baseline', () => {
        const logs = Log.get('test.scope-baseline');
        expect(logs.debugLog).toBeNull();
        expect(logs.infoLog).toBeNull();
        expect(logs.warnLog).not.toBeNull();
        expect(logs.errorLog).not.toBeNull();
    });

    it('records every requested scope in knownScopes', () => {
        Log.get('test.a');
        Log.get('test.b');
        expect(Log.knownScopes.has('test.a')).toBe(true);
        expect(Log.knownScopes.has('test.b')).toBe(true);
    });

    it('respects scopeDefaults set by createLogProvider', () => {
        type S = 'A' | 'B';
        const getLogs = createLogProvider<S>('pkg.', { A: LogLevel.Debug, B: LogLevel.Error });
        expect(Log.scopeDefaults.get('pkg.A')).toBe(LogLevel.Debug);
        expect(Log.scopeDefaults.get('pkg.B')).toBe(LogLevel.Error);

        const a = getLogs('A');
        expect(a.debugLog).not.toBeNull();
        expect(a.infoLog).not.toBeNull();

        const b = getLogs('B');
        expect(b.debugLog).toBeNull();
        expect(b.warnLog).toBeNull();
        expect(b.errorLog).not.toBeNull();
    });

    it('explicit minLevels override scopeDefaults', () => {
        type S = 'A';
        const getLogs = createLogProvider<S>('pkg2.', { A: LogLevel.Debug });
        Log.minLevels.set('pkg2.A', LogLevel.Error);
        const a = getLogs('A');
        expect(a.debugLog).toBeNull();
        expect(a.warnLog).toBeNull();
        expect(a.errorLog).not.toBeNull();
    });

    it('debugLog?.log routes to console.debug; warnLog to console.warn', () => {
        Log.minLevels.set('pkg3.A', LogLevel.Debug);
        const debugSpy = vi.spyOn(console, 'debug').mockImplementation(() => undefined);
        const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => undefined);

        const { debugLog, warnLog } = Log.get('pkg3.A');
        debugLog?.log('hello', 1);
        warnLog?.log('boom');

        expect(debugSpy).toHaveBeenCalledWith('[pkg3.A]', 'hello', 1);
        expect(warnSpy).toHaveBeenCalledWith('[pkg3.A]', 'boom');
    });

    it('logLevels controller is installed on globalThis after first Log.get', () => {
        Log.get('test.controller');
        const ctrl = (globalThis as Record<string, unknown>).logLevels;
        expect(ctrl).toBeDefined();
        // The controller exposes the public methods used from the dev console
        expect(typeof (ctrl as { override: unknown }).override).toBe('function');
        expect(typeof (ctrl as { dump: unknown }).dump).toBe('function');
        expect(typeof (ctrl as { overrideAll: unknown }).overrideAll).toBe('function');
    });
});
