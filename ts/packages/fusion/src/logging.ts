import { createLogProvider, LogLevel } from '@actuallab/core';

// Per-package log scopes.  Use createLogProvider to get a typed helper:
//   const { debugLog, warnLog } = getLogs('Computed');
// Final scope name is 'fusion.Computed' — the prefix isolates this package's
// scopes from other packages' scopes when overriding levels at runtime.
export type FusionLogScope =
    | 'Computed'
    | 'ComputedRegistry'
    | 'ComputedState'
    | 'ComputeFunction'
    | 'MutableState'
    | 'State'
    | 'UIActionTracker'
    | 'UIUpdateDelayer';

// Per-scope defaults.  All Warn — Fusion is mostly hot-path compute graph code;
// noisy scopes need to be opted-in for development.
const scopeDefaults: Record<FusionLogScope, LogLevel> = {
    Computed: LogLevel.Warn,
    ComputedRegistry: LogLevel.Warn,
    ComputedState: LogLevel.Warn,
    ComputeFunction: LogLevel.Warn,
    MutableState: LogLevel.Warn,
    State: LogLevel.Warn,
    UIActionTracker: LogLevel.Warn,
    UIUpdateDelayer: LogLevel.Warn,
};

export const getLogs = createLogProvider<FusionLogScope>('fusion.', scopeDefaults);
