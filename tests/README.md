# Test suite: structure, groups, and run modes

## Test projects

- `ActualLab.Tests` — core libraries + RPC ("core" and "rpc" groups)
- `ActualLab.Fusion.Tests` — Fusion ("fusion" group)
- `*.PerformanceTestRunner` / `*.BenchmarkRunner` — console runners, not part of `dotnet test` runs

## Groups and categories

Tests are grouped via xUnit traits and collections:

| Group | Definition |
|---|---|
| `core` | `ActualLab.Tests` with `--filter "Category!=Rpc"` |
| `rpc` | `ActualLab.Tests` with `--filter "Category=Rpc"` |
| `fusion` | `ActualLab.Fusion.Tests` (whole project) |

Orthogonal to groups:

- **`TimeSensitiveTests` collection** (`[Collection(nameof(TimeSensitiveTests))]`, paired with
  `Trait("Category", nameof(TimeSensitiveTests))`) — tests with real-time assertions (clocks, timers,
  reconnection, keep-alive, delayed events). The collection runs sequentially and exclusively within
  its assembly. Mark a class this way only if it genuinely asserts wall-clock timings; everything else
  should stay parallel-friendly. Filter with `Category=TimeSensitiveTests` / `Category!=TimeSensitiveTests`.
- **Full-run-only tests** (`[FullRunOnlyFact]` / `[FullRunOnlyTheory]`) — benchmarks and performance
  tests. Skipped unless `ActualLab_FullTestRun=1`. Use these attributes for any new benchmark-style test.

## Run modes and environment variables

| Variable | Effect |
|---|---|
| `ActualLab_FullTestRun=1` | Runs `FullRunOnly*` tests (benchmarks, perf) and expands the RPC serialization format matrix to all formats |
| `ActualLab_Tests_DbTypes=Sqlite,PostgreSql,...` | Explicit list of enabled `FusionTestDbType` values; disables port probing |

### RPC serialization format matrix

Format-matrix theories (see `RpcTestFormats`) run a reduced core set by default:
`json5`, `njson5`, `json5np`, `mempack6c`, `msgpack6c`, `nmsgpack6c` — one per serializer family.
The full 14-format matrix (v5 wire format, non-compact variants, `njson5np`) runs on build agents
and when `ActualLab_FullTestRun=1`. **Skipping the legacy/duplicate formats in local runs is an
accepted trade-off** — full coverage is provided by CI and pre-release full runs.

### Databases

Locally, DB-backed Fusion tests expect **SQLite + InMemory + PostgreSQL** (`docker-compose up -d`
provides PostgreSQL and Redis). MariaDB and SQL Server variants probe `127.0.0.1:3306` / `:1433`
with a short TCP timeout and **skip automatically** when those servers aren't running — it's normal
and expected that they don't run in local/agent test passes. Build agents use InMemory only.

## Running the suite fast

Use the build app's `test` target — from the repository root:

```
Run-Tests.cmd [core|rpc|fusion|all] [fast|full]       # default: all fast
Build.cmd test --test-scope <scope> --test-mode <mode> # same thing, generic entry point
dotnet run --project build -c Release --no-launch-profile -- test --test-scope <scope> --test-mode <mode>
```

The target:

1. Kills stale `testhost` processes of this repo/worktree (they hold DLL locks that fail builds).
2. Builds the in-scope test projects, then runs every group with `--no-build`.
3. Phase A — parallel processes: `core`, `rpc`, and `fusion` groups, each excluding
   `Category=TimeSensitiveTests` (and `PerformanceTests`).
4. Phase B — the `Category=TimeSensitiveTests` slice of each project, sequentially and exclusively.
5. Prints a per-group summary + failed test list; logs and TRX files go to
   `artifacts/tests/runs/<timestamp>/`.
