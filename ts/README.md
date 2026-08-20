# Fusion for TypeScript

[![Documentation](https://img.shields.io/badge/Documentation-6B5B95)](https://fusion.actuallab.net/PartTS)
[![Samples](https://img.shields.io/badge/Samples-88B04B)](https://github.com/ActualLab/Fusion.Samples)
[![Changelog](https://img.shields.io/badge/Changelog-4A4A4A)](https://fusion.actuallab.net/CHANGELOG)

This is the npm workspace for the TypeScript port of
[ActualLab.Fusion](https://github.com/ActualLab/Fusion). Its goal: let a TypeScript/JavaScript UI
consume Fusion Compute Services and `ActualLab.Rpc` services running on a .NET server, and get
real-time, invalidation-driven updates — the same way Fusion + Blazor works on .NET.

The port is deliberately lighter than the .NET version. Server-side-only features (CommandR,
Operations Framework, EF extensions, Authentication) stay on the .NET server where they belong.

## Packages

| Package | Description | .NET counterpart |
|---------|-------------|------------------|
| [`@actuallab/core`](packages/core) | `Result`, `AsyncContext`, `AsyncLock`, `PromiseSource`, events, retry | `ActualLab.Core` |
| [`@actuallab/rpc`](packages/rpc) | `RpcHub`, `RpcClientPeer`, `RpcStream`, WebSocket transport | `ActualLab.Rpc` |
| [`@actuallab/fusion`](packages/fusion) | `Computed<T>`, `@computeMethod`, `ComputedState`, `MutableState` | `ActualLab.Fusion` |
| [`@actuallab/fusion-rpc`](packages/fusion-rpc) | `FusionHub` — compute-aware RPC with invalidation propagation | `ActualLab.Fusion` (client part) |
| [`@actuallab/fusion-react`](packages/fusion-react) | `useComputedState`, `useMutableState` | `ActualLab.Fusion.Blazor` |

All packages are ESM-first (with a CJS fallback), MIT-licensed, built with `tsup`, and tested with
`vitest`.

## Using them in an app

```bash
npm install @actuallab/fusion-rpc @actuallab/fusion-react
```

Each package's README has a quick start; the [TypeScript port
docs](https://fusion.actuallab.net/PartTS) cover the architecture, the `AsyncContext` rules, and
every difference from .NET. The [TodoApp
sample](https://github.com/ActualLab/Fusion.Samples/tree/master/src/TodoApp) is a complete
React + Fusion client.

## Working on this workspace

```bash
npm install          # or: Npm-Install.cmd
npm run build        # tsup build of all 5 packages, in dependency order
npm test             # vitest run (or: Run-Tests.cmd)
npm run test:watch
npm run typecheck    # tsc -p tsconfig.typecheck.json
npm run lint         # eslint (Run-Lint.cmd builds first)
npm run clean
```

Layout:

- `packages/*` — the published packages; tests live in each package's `tests/` folder
- `e2e/` — cross-language scripts driven from .NET tests (`ts-dotnet-e2e.ts`,
  `ts-dotnet-perf.ts`), which run a TypeScript client against a real .NET RPC server
- `Publish.ps1` / `Publish.cmd` — release scripts

Publishing goes through the repo's release flow, which bumps all workspace versions together — see
the [changelog](https://fusion.actuallab.net/CHANGELOG) for the npm version line of each release.

## License

MIT — see [LICENSE](../LICENSE).
