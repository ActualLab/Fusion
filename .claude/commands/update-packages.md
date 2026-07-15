---
allowed-tools: Read, Edit, Bash, Grep, WebFetch
description: Update NuGet package versions in Directory.Packages.props following Fusion's pinning rules
argument-hint: [optional: package name(s) or "all"]
---

# NuGet Package Updater

Update package versions in [`Directory.Packages.props`](../../Directory.Packages.props)
(central package management — every version lives in this one file). Follow the rules
below exactly; the whole point is that **most framework versions must stay low on
purpose**, so a blind "bump everything to latest" is wrong here.

`$ARGUMENTS` may name specific package(s) to update. If empty, review all packages
against the rules and update the ones the rules say to update.

## The two version idioms (this is the crux)

`Directory.Packages.props` expresses versions in two deliberately different ways:

1. **Floating floor — `[X.Y.Z,)`** (a NuGet version range meaning "X.Y.Z or higher").
   This is a **lower bound**, not a target. It's the *minimum* version Fusion supports.
   A consumer app that references Fusion **and** a newer version of the same package
   automatically resolves to the newer one — NuGet picks the highest floor across the
   graph. Raising this floor does not "upgrade" anyone; it only **shrinks the range of
   versions Fusion is compatible with**, potentially forcing consumers off versions they
   were happily using. **Leave most of these floors alone** — the exception is the
   crucial packages below, whose floors we *do* keep current.

2. **Exact pin — `X.Y.Z`** (or a `...Version` property holding an exact value).
   This is a hard target: Fusion builds, tests, and ships against exactly this version.
   Used for test/build/analyzer/samples packages. Bump these to the **latest stable** release.

Both crucial framework packages (MessagePack, Redis) and the leave-alone platform packages
use the **floor idiom** — the difference is not the form but whether we keep the floor
current. For a crucial package you **raise the floor to the latest stable**
(`[3.1.6,)` → `[3.2.0,)`); for a platform package you leave the floor where it is.
Never convert a crucial package to an exact pin — keep it `[X,)`.

Most versions are set via `...Version` properties near the top of the file (e.g.
`MessagePackVersion`, `RedisVersion`, `OpenTelemetryVersion`). Edit the property, not the
individual `PackageVersion` lines that reference it.

## Update rules

### DO update to latest stable

- **Test-time-only** packages (`<!-- Test time only -->` section): xunit, AwesomeAssertions,
  Moq, Castle.Core, coverlet.collector, Microsoft.NET.Test.Sdk, FlakyTest.XUnit,
  AutoFixture.AutoMoq, ILogger.Moq, xunit.runner.visualstudio, etc.
- **Analyzers** and **Generators** sections: Roslynator.Analyzers, Meziantou.Analyzer,
  Moq.Analyzers, xunit.analyzers, Microsoft.VisualStudio.Threading.Analyzers,
  Microsoft.CodeAnalysis.Analyzers.
- **Build-time-only** tooling: Nerdbank.GitVersioning, PolySharp, Microsoft.SourceLink.GitHub.
- **Samples-only** packages (`<!-- Samples only -->`, `<!-- Build & Samples -->`,
  `<!-- Used only in TodoApp sample -->`): AspNet.Security.OAuth.GitHub, Blazored.LocalStorage,
  UAParser, Pastel, Bullseye, CliWrap, System.CommandLine\*, Aspire (`AspireSdkVersion`),
  Blazorise (`BlazoriseVersion`). Anything referenced **only** from `samples/` is always
  safe to take to latest.
- **Crucial framework packages** — even though they're framework deps, keep these current
  because wire format / infra behavior matters and Fusion tests against a recent version:
  **MessagePack** (`MessagePackVersion`) and **StackExchange.Redis** (`RedisVersion`).
  These stay in the **floor idiom** `[X,)`, but you **raise the floor to the latest stable**
  (unlike the platform floors below, which you leave low). Don't convert them to exact pins.

### DO NOT update (unless explicitly asked, with a reason)

- **Floating-floor framework packages** — the `[X,)` entries: ASP.NET Core / Blazor
  (`AspNetCoreVersion*`, `BlazorVersion*`), EF Core + providers (`EntityFrameworkCoreVersion*`,
  `Npgsql*`, `MySqlConnectorVersion*`, `Pomelo.EntityFrameworkCore.MySql`),
  `Microsoft.Extensions.*` (`MicrosoftExtensionsVersion*`) and the `System.*` packages that
  track it, Newtonsoft.Json, MemoryPack, Nerdbank.MessagePack, `MessagePackVersion2`
  (the v2 line), RestEase, OpenTelemetry. Leave the floors low.
- **Per-TFM minimum variants** — any property suffixed with a number (`...Version9`,
  `...Version8`, `...Version3`, etc.). These are the minimum supported version *for that
  target framework* and are load-bearing for multitargeting. Never touch.
- **Legacy-support** packages (`<!-- Legacy support only -->`): IndexRange,
  Microsoft.Bcl.AsyncInterfaces, System.Memory, Microsoft.AspNet.WebApi.\*, Microsoft.Owin,
  Owin, System.Reflection.Emit.Lightweight, MsgPack.Cli, the `System.Net.*` 4.3.x pins,
  System.Runtime.Loader. Pinned for old-TFM / netfx / netstandard back-compat; bumping
  can break those targets.
- **`FusionVersion`** and the `ActualLab.*` self-references it feeds — that's Fusion's own
  published version (samples reference the released packages). It's managed by the release
  process, not a routine package update. Leave it unless you're explicitly doing a release bump.

### Always respect inline rationale comments

Several pins carry a `<!-- ... -->` explaining *why* they're held back, e.g.:
- `Xunit.DependencyInjection.Logging` held at 8.x because 9.0.0 removed `XunitTestOutputLoggerProvider`.
- CommunityToolkit.HighPerformance was deliberately downgraded (see git history) for compatibility.

If a package has such a comment, **do not override it** without confirming the reason no
longer applies. When you skip a package for this reason, say so in your summary.

## Procedure

1. **Discover candidates.** Restore, then list outdated packages:
   ```bash
   dotnet restore ActualLab.Fusion.sln
   dotnet list ActualLab.Fusion.sln package --outdated
   ```
   (If offline, check each candidate's latest stable on nuget.org via WebFetch:
   `https://api.nuget.org/v3-flatcontainer/{lowercase-id}/index.json`.)
2. **Filter by the rules above.** Only the "DO update" categories are in scope. Read the
   section comment each package sits under before touching it.
3. **Edit `Directory.Packages.props`.** Prefer editing the shared `...Version` property when
   one exists. Keep exact pins exact; keep floors as floors.
4. **Build.** No CI solution filter exists in this repo, so build the full solution:
   ```bash
   dotnet build ActualLab.Fusion.sln
   ```
   If a serialization or infra package changed (MessagePack, Redis, etc.), also run the
   relevant tests, not just the build.
5. **Fix or revert on failure.** If a bump breaks the build/tests and the fix isn't
   obvious, revert that one package and note it — don't chase unrelated refactors.
6. **Commit to `master`** (no feature branch — see project rules) with a clear message,
   e.g. `chore: update <packages> in Directory.Packages.props`. Group related bumps;
   keep unrelated ones separate when practical (matches the repo's existing chore/dependabot history).

## Summary to report

List, in three buckets: **updated** (old → new), **skipped by rule** (which rule), and
**skipped by inline comment** (which comment). Then state the build/test result.
