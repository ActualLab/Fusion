// TC39 decorator metadata polyfill — required for Symbol.metadata support
// in runtimes that don't yet implement it natively.
(Symbol as any).metadata ??= Symbol("Symbol.metadata");
