// TC39 decorator metadata polyfill — required for Symbol.metadata support
// in runtimes that don't yet implement it natively.
// eslint-disable-next-line @typescript-eslint/no-explicit-any, @typescript-eslint/no-unsafe-member-access
(Symbol as any).metadata ??= Symbol('Symbol.metadata');
