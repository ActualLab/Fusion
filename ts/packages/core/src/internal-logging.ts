import { LogLevel, createLogProvider } from './logging.js';

/** Log scopes used inside `@actuallab/core` itself. Each is registered at
 *  `core.<scope>`. Keep the list tight — most utilities should propagate
 *  errors rather than log them. */
export type CoreLogScope =
    | 'retry'
    | 'throttle';

const scopeDefaults: Record<CoreLogScope, LogLevel> = {
    retry: LogLevel.Warn,
    throttle: LogLevel.Warn,
};

export const getLogs = createLogProvider<CoreLogScope>('core.', scopeDefaults);
