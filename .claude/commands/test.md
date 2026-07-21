---
allowed-tools: Bash, PowerShell, Read, Grep, Glob
description: Run the test suite with parallel runners (scopes: core|fusion|rpc|all; modes: fast|full)
argument-hint: [core|fusion|rpc|all] [fast|full]
---

# Parallel test runner

Run the test suite via the build app's `test` target, which splits tests into groups running in
parallel processes and isolates time-sensitive tests into an exclusive final phase.
Background: [`tests/README.md`](../../tests/README.md).

`$ARGUMENTS` holds up to two tokens: a **scope** (`core`, `rpc`, `fusion`, `all`; default `all`)
and a **mode** (`fast`, `full`; default `fast`). A bare `/test` means `all fast`.

## Run

Route the run to the build app (both forms are equivalent; use the second on non-Windows):

```
./Run-Tests.cmd [scope] [mode]
dotnet run --project build -c Release --no-launch-profile -- test --test-scope <scope> --test-mode <mode>
```

Expect ~5-10 minutes for `all fast` (use a 15-minute timeout; run in background and monitor).
The target prints a per-group summary (name, failed/passed/skipped, time) plus the list of failed
tests, and stores full logs + TRX files in `artifacts/tests/runs/<timestamp>/`.

Notes:
- `full` mode also runs benchmarks/perf tests and the full 14-format RPC matrix; it's much slower
  and CPU-hungry. Warn the user if PostgreSQL (port 5432) isn't reachable in full mode.
- The target kills stale `testhost` processes of the current repo/worktree automatically and builds
  the test projects itself — no separate build step is needed.

## Failure handling

If the summary reports failures:
1. Read the failing group's log in the run's results directory to get the errors.
2. Rerun just the failed tests once, serially, with nothing else running:
   `dotnet test <project> -c Debug -f net10.0 --no-build --filter "FullyQualifiedName~<Class1>|FullyQualifiedName~<Class2>"`
3. Tests that pass on the serial rerun are **flaky under parallel load** — report them separately
   (naming the class), and consider suggesting a `TimeSensitiveTests` collection marker for repeat
   offenders. Tests that fail again are real failures — report them with their error output.

## Report

Report per-group results, the flaky-vs-real failure split, total wall-clock time, and the results
directory path.
