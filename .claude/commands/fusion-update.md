---
allowed-tools: Read, Edit, Bash, Grep, Glob, WebFetch, mcp__fusion-docs__search, mcp__fusion-docs__get, mcp__fusion-docs__source_search, mcp__fusion-docs__source_read
description: Bump sibling projects to the latest Fusion (NuGet + npm), then build & fix each
argument-hint: "[samples|everywhere]  (default: everywhere)"
---

# Update Fusion in sibling projects

Bring **other** projects that depend on ActualLab.Fusion up to the **latest released
Fusion version** — both the **NuGet** packages (`ActualLab.*`) and the **npm** packages
(`@actuallab/*`) — then build each updated project and fix any breakage the bump caused,
using Fusion's changelog and commit log as the source of truth.

This command runs **from the Fusion repo** but edits **sibling repos**. It never bumps
Fusion itself — Fusion is the source of the new version, not a consumer of it.

`$ARGUMENTS` selects the **scope**:

- **`everywhere`** *(default — also the value for an empty/unrecognized argument)*: every
  sibling project that has an `AGENTS-Source.md` **and** references Fusion. Includes the
  real apps (e.g. `ActualChat`) as well as the samples.
- **`samples`**: only Fusion's **sample / demo** projects — the ones whose purpose is to
  showcase Fusion (currently `ActualLab.Fusion.Samples`, `ActualLab.Fusion.TownHall`,
  `BoardGames`). Excludes production apps like `ActualChat`. Use this for a quick, low-risk
  refresh of the demos.

## Reuse

- **Existing abstractions to reuse.** Version-bump mechanics mirror
  [`packages-update`](packages-update.md) (central package management via
  `Directory.Packages.props`, editing a shared `...Version` property rather than each
  `PackageVersion` line). Fusion's own **npm** package names are the ones under
  `ts/packages/*` (`@actuallab/core`, `@actuallab/rpc`, `@actuallab/fusion`,
  `@actuallab/fusion-rpc`, `@actuallab/fusion-react`). To understand breaking changes, reuse
  the `fusion-docs` MCP (source/docs search) and this repo's `docs/CHANGELOG.md` + `git log`.
- **Reusability of new components.** This command introduces no shared code — it only edits
  sibling projects' package manifests, so there's nothing to promote to `ActualLab.Core` /
  `ts/actuallab-core`.

## Step 1 — Determine the target versions (do this once)

You need two numbers: the latest **NuGet** version and the latest **npm** version. Take the
**higher** of what NuGet/npm publishes and what Fusion's changelog states.

1. **Fusion changelog** — the top `##` header in this repo's
   [`docs/CHANGELOG.md`](../../docs/CHANGELOG.md) is authoritative and maps both:
   ```
   ## 13.0.101+4292afe9 | npm: 13.0.25
        └ NuGet ┘└ commit ┘        └ npm ┘
   ```
   Read it: `grep -m1 '^## ' docs/CHANGELOG.md`.
2. **NuGet published** — latest **stable** (non-prerelease) of `ActualLab.Fusion`:
   ```bash
   curl -s https://api.nuget.org/v3-flatcontainer/actuallab.fusion/index.json
   ```
   Take the last entry that is not a prerelease.
3. **npm published** — latest of `@actuallab/fusion`:
   ```bash
   curl -s https://registry.npmjs.org/@actuallab/fusion/latest
   ```
   Read `.version`.
4. Let **`NUGET_VER = max(changelog NuGet, nuget published)`** and
   **`NPM_VER = max(changelog npm, npm published)`** (semver comparison). Report both before
   editing anything.

## Step 2 — Find the target projects

The sibling root is the parent of this repo (`AC_ProjectRoot`, e.g. `/proj`).

**Start from this known list** (verified 2026-07-15) so you don't have to re-scan every
sibling on each run — just confirm each still exists and still references Fusion, then apply
the scope filter:

| Project (folder)            | Scope       | NuGet pin (`Directory.Packages.props`)      | npm (`@actuallab/*`) | Branch |
|-----------------------------|-------------|---------------------------------------------|----------------------|--------|
| `ActualChat`                | everywhere  | `<ActualLabFusionVersion>` (root)           | none                 | `dev`  |
| `ActualLab.Fusion.Samples`  | samples     | `<ActualLabFusionVersion>` (root)           | yes (TodoApp TS UI)  | `master` |
| `ActualLab.Fusion.TownHall` | samples     | `<ActualLabFusionVersion>` (root)           | none                 | `main` |
| `BoardGames`                | samples     | `<ActualLabFusionVersion>` (`src/`)         | none                 | `main` |

**Skipped by rule (don't bump):**
- `NativeAotQuirks` — references `ActualLab.Core` with `Version="*"` (already floating).
- `ActualChat-C1`, `ActualChat-C2`, and any other `ActualChat-<suffix>` — see below.

Then, before editing, quickly re-validate and adjust:

- A candidate must have an `AGENTS-Source.md` **and** reference Fusion — either a
  `Directory.Packages.props`/`*.csproj` mentioning `ActualLab.*` (NuGet) **or** a
  non-`node_modules` `package.json` mentioning `@actuallab/*` (npm). If a **new** sibling now
  qualifies (not in the table), include it and mention it in the summary.
- **Exclude the Fusion repo itself** (`ActualLab.Fusion`) — it's the source.
- **ActualChat: update the main folder only.** There are usually several sibling **checkouts**
  of ActualChat — the plain `ActualChat` plus suffixed ones like `ActualChat-C1`,
  `ActualChat-C2` (separate clones, each with its own `.git`, not git worktrees). **Only the
  suffix-less `ActualChat` is updated**, and it lives on the **`dev`** branch (not `master`) —
  make sure you're on `dev` there. List the skipped suffixed checkouts in the summary.
- **For `samples` scope**, keep only the rows marked `samples`; drop production apps
  (`ActualChat`) and repro/quirk projects.
- **Skip pins you can't safely bump** — e.g. a floating `Version="*"` (like `NativeAotQuirks`);
  leave it and note it.

Report the final list and wait only if it's empty or clearly wrong; otherwise proceed.

## Step 3 — Update each project

Process projects one at a time. For each:

### 3a. NuGet (`ActualLab.*`)

Find where the version is pinned (usually central package management):

- Preferred: a shared property in `Directory.Packages.props` — commonly
  `<ActualLabFusionVersion>` (samples/BoardGames/TownHall/ActualChat) or `<FusionVersion>`.
  The props file may be at the repo root **or** under `src/`. Edit the **property**, set it to
  `NUGET_VER`. That single edit updates every `ActualLab.*` `PackageVersion` that references it.
- If there's no such property, bump the `Version="..."` on each `ActualLab.*`
  `PackageVersion`/`PackageReference` line directly. **Never** touch a `Version="*"` line.

### 3b. npm (`@actuallab/*`)

Search the project for non-`node_modules` `package.json` files mentioning `@actuallab/`. For
each, set every `@actuallab/*` dependency to `^NPM_VER` (keep the existing range operator —
these use `^`). Not every project has npm deps (e.g. ActualChat syncs TS from source and has
none) — that's fine, skip npm for those.

## Step 4 — Restore, build, and fix each project

For each updated project (still one at a time):

1. **Read the project's own build instructions first.** Open its `AGENTS.md` /
   `AGENTS-Source.md` / `README` for the exact build command and any solution filter. Prefer a
   `*.CI.slnf` if one exists (per the general build rule); otherwise the `.sln`.
2. **.NET**: `dotnet restore` then build:
   ```bash
   dotnet build <Project>.CI.slnf   # or <Project>.sln if no CI filter
   ```
3. **npm/TS** (only if 3b changed something): `npm install` (refreshes the lockfile to the new
   `@actuallab/*` versions) then the project's TS build/lint (e.g. `npm run build`).
4. **Fix breakage caused by the bump** using Fusion's own history — do **not** guess:
   - Read `docs/CHANGELOG.md` **between the project's old version and `NUGET_VER`**, focusing
     on **Breaking Changes** / **Changed** sections. (Recent example breaking changes: compute
     services with command handlers must be registered as **singletons**;
     `DisableAutoTransactionsAndSavepoints()` → `DisableAutoTransactions(allowSavepoints:)`.)
   - Each changelog header embeds the commit hash (`X.Y.Z+hash`), so for details run
     `git log <oldHash>..<newHash>` in **this** (Fusion) repo, or inspect the source via the
     `fusion-docs` MCP (`source_search` / `source_read`) and `docs/` search.
   - Apply the migration each breaking change prescribes. Follow the **target** project's
     coding style, not Fusion's.
5. If a failure is **not** explained by a Fusion breaking change (unrelated to the bump), don't
   chase it — note it and move on. If the fix is genuinely unclear after consulting the
   changelog + commit log, stop and ask.

## Step 5 — Summary (do not auto-commit)

These are **separate repositories**. Make edits in each working tree but **do not commit or
branch** unless the user asks. Report, per project:

- old → new **NuGet** version, and old → new **npm** version (or "no npm").
- build result (✅ / ❌) and, for each fix applied, the breaking change + changelog version it
  came from.
- anything **skipped** and why (worktree dupe, floating `*` pin, no AGENTS-Source, out of
  scope, unrelated failure).

Finish with the resolved `NUGET_VER` / `NPM_VER` and the scope you ran.
