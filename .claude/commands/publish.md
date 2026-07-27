---
allowed-tools: Read, Edit, Bash, Glob, Grep, WebFetch, AskUserQuestion, Skill
description: Publish NuGet (.NET) and/or npm (TS) packages; auto-detects what changed, tests via /test, then updates the changelog
argument-hint: "[.net|ts|both|auto] [-test|-no-test] [-changelog|-no-changelog]"
---

# Publish

Publish ActualLab.Fusion packages to NuGet (.NET) and/or npm (TS).

**Publishing is a critical, effectively irreversible action** — a pushed package
version can't be replaced, and consumers pick it up immediately. So the bar for
guessing is much higher here than in normal work: **if you're in doubt about
anything that may negatively impact the outcome** — an unexpected diff or version,
a partially failed push, a dirty working tree, a test you're tempted to shrug off,
a workaround you're about to invent — **stop and ask the user** (AskUserQuestion)
how to resolve it instead of picking an answer yourself. A pause costs a minute;
a wrong publish costs a version.

## Instructions

### Step 1: Parse arguments

`$ARGUMENTS` holds a target and flags; **the default is `auto -test -changelog`**.

Target:
- `.net`, `dotnet`, or `net` → publish .NET (NuGet) only
- `ts` → publish TypeScript (npm) only
- `both`, `all` → publish both (order: .NET first, then TS)
- `auto` (or no target) → detect what needs publishing (Step 3)

Flags:
- `-test` (default) / `-no-test` → run or skip the pre-publish test pass (Step 4)
- `-changelog` (default) / `-no-changelog` → run or skip `/changelog-update` after
  publishing (Step 6)

### Step 2: Check the branch and the environment

**Publish only from `master`, fully in sync with `origin/master`.** `version.json`
lists `^refs/heads/master$` as the only `publicReleaseRefSpec`, so any other branch
stamps a git-hash version like `14.1.64-g59e65c53cc` — and pushing that to NuGet
burns a bogus version permanently. Verify all of the following:

```powershell
git rev-parse --abbrev-ref HEAD          # must be: master
git status --short                       # must be empty
git fetch origin
git rev-list --left-right --count origin/master...master   # must be: 0    0
dotnet nbgv get-version -v NuGetPackageVersion             # must have no -g<hash> suffix
```

Resolve problems as follows:

- **Not on `master`** → STOP and ask the user (AskUserQuestion) whether to switch,
  or whether they meant to publish something else. Never switch branches on your own.
- **Dirty working tree** → STOP and ask. Never stash, discard, or commit on your own.
- **Behind `origin/master`** → run `git pull --ff-only`; if it isn't a fast-forward,
  STOP and ask.
- **Ahead of `origin/master`** → the commits being published must be on the remote
  first. Push them (`git push`) before publishing, and say what you pushed.
- **Diverged** (both counts non-zero) → STOP and ask; don't rebase or merge on your own.
- **Version still has a `-g<hash>` suffix on master** → STOP and ask; something is
  off with the nbgv setup and the publish would be wrong.

Then check the environment. Publishing runs `.cmd` scripts and needs host credentials
(`ActualChat_NuGet_API_Key`, `ActualLab_NPM_Key`), so it must run on the
host OS. **This is a hard requirement for .NET publishing.**

Check the `AC_OS` environment variable:

- Unset, `Windows`, `macOS`, or `Linux` → OK, proceed.
- `Linux in Docker`, `Linux on WSL`, or anything else sandboxed → STOP and
  tell the user to re-run the publish from an agent started on the host OS
  (`ai os`). Do not try to work around this.

### Step 3: Auto target detection (target `auto` only)

Figure out what actually changed since the last publish, per artifact family:

1. **Find the latest published versions:**
   - Check the changelog (see the `/changelog-update` skill for its location) for the
     most recent published version entries.
   - Verify directly: NuGet — `dotnet package search ActualLab.Core --exact-match`
     (or fetch `https://api.nuget.org/v3-flatcontainer/actuallab.core/index.json` and
     take the last entry); npm — `npm view @actuallab/core version` (run `npm view`
     on one of the `ts/packages/*` package names).
2. **Map each version to its commit:** `dotnet nbgv get-commits <version>` prints the
   commit(s) matching a version. Cross-check with the changelog entry's commit if
   ambiguous.
3. **Diff for relevant changes** from that commit to `HEAD`:
   - .NET packages: `git log <commit>..HEAD --oneline -- src/ Directory.Packages.props
     Directory.Build.props version.json` (anything under `src/` ships in packages;
     ignore `docs/`, `tests/`, `samples/`, `.claude/`).
   - TS packages: `git log <commit>..HEAD --oneline -- ts/packages/` (ignore `ts/e2e/`,
     TS test/config-only changes that don't ship: judge by the file lists).
4. **Decide:**
   - Changes in both → publish both.
   - Changes only in TS artifacts → publish (and test) TS only.
   - Changes only in .NET artifacts → publish (and test) .NET only.
   - No relevant changes anywhere → **skip publishing entirely**; report what was
     checked (versions, commits, diff summary) and stop.

Report the decision (with the version→commit mapping and a one-line diff summary)
before proceeding.

### Step 4: Pre-publish tests (skip with `-no-test`)

Only test the targets being published:

- **.NET**: run the parallel test suite via the `/test` skill (equivalently:
  `./Run-Tests.cmd` from the repository root — `all fast` mode). It must be green;
  flaky-but-passing-on-rerun tests (as classified by `/test`) don't block publishing.
- **TS**: from the `ts/` folder run `./Run-Lint.cmd`, then `./Run-Tests.cmd`
  (vitest). Both must pass.

If something fails: fix the errors, re-run the failed part, and repeat until green.
Don't skip or suppress failures. (With `-no-test`, still run `ts/Run-Lint.cmd`
before a TS publish — it's fast and catches build breaks.)

### Step 5: Publish

- **.NET**: run `./Publish.cmd` from the `build/` folder. It packs with
  `PUBLIC_BUILD=1` and pushes every package from `artifacts/nupkg/` to
  NuGet.org.
- **TS**: run `./Publish.cmd` from the `ts/` folder. It stamps the nbgv
  version into `packages/*/package.json`, builds, and publishes all
  `@actuallab/*` workspaces to npm.

Use a generous Bash timeout (10 minutes) — packing and pushing take a while.

If a publish fails:

- **Build/compile errors** → fix them, run the tests targeting the fixed
  code (e.g., `/test` for .NET, `ts/Run-Tests.cmd` for TS), then re-run the
  publish script.
- **Credential/auth errors** (missing API key, `npm whoami` failure, scope
  access) → report to the user and stop; never work around credentials.
- **Transient push errors** → the scripts already retry; re-run once before
  reporting.

### Step 6: Changelog update (skip with `-no-changelog`)

Determine the published version first — NuGet pushes are NOT instantly
visible on NuGet.org, so `/changelog-update` must be told the version
explicitly instead of detecting it:

```powershell
dotnet nbgv get-version -v NuGetPackageVersion
```

(The TS publish output also prints `Published version X` — it uses the same
nbgv version.)

Invoke the `changelog-update` skill passing that exact version as its
argument, and mention in its input which targets (NuGet, npm, or both) were
just published.
