---
allowed-tools: Read, Edit, Bash, Glob, Grep, AskUserQuestion, Skill
description: Publish NuGet (.NET) and/or npm (TS) packages, then offer /changelog-update
argument-hint: [.net|ts|both] [and update changelog]
---

# Publish

Publish ActualLab.Fusion packages to NuGet (.NET) and/or npm (TS).

## Instructions

### Step 1: Parse arguments

`$ARGUMENTS` selects the publish target:

- `.net`, `dotnet`, or `net` → publish .NET (NuGet) only
- `ts` → publish TypeScript (npm) only
- `both`, `all` → publish both (order: .NET first, then TS)
- If no target is given, ask the user (AskUserQuestion) which one to publish:
  .NET / TS / Both.

If the arguments also express the intent to update the changelog (e.g.,
"and update changelog"), remember it: Step 5's question is pre-answered
with "yes" and must NOT be asked.

### Step 2: Check the environment

Publishing runs `.cmd` scripts and needs host credentials
(`ActualChat_NuGet_API_Key`, `ActualLab_NPM_Key`), so it must run on the
host OS. **This is a hard requirement for .NET publishing.**

Check the `AC_OS` environment variable:

- Unset, `Windows`, `macOS`, or `Linux` → OK, proceed.
- `Linux in Docker`, `Linux on WSL`, or anything else sandboxed → STOP and
  tell the user to re-run the publish from an agent started on the host OS
  (`ai os`). Do not try to work around this.

### Step 3: TS pre-publish checks (TS and Both targets only)

Always run these from the `ts/` folder before publishing, in order:

```powershell
./Run-Lint.cmd    # npm run build && npm run lint
./Run-Tests.cmd   # vitest run
```

Both must pass. If either fails: fix the errors, re-run the failed script,
and repeat until green. Don't skip or suppress failures.

### Step 4: Publish

- **.NET**: run `./Publish.cmd` from the `build/` folder. It packs with
  `PUBLIC_BUILD=1` and pushes every package from `artifacts/nupkg/` to
  NuGet.org.
- **TS**: run `./Publish.cmd` from the `ts/` folder. It stamps the nbgv
  version into `packages/*/package.json`, builds, and publishes all
  `@actuallab/*` workspaces to npm.

Use a generous Bash timeout (10 minutes) — packing and pushing take a while.

If a publish fails:

- **Build/compile errors** → fix them, run the tests targeting the fixed
  code (e.g., the matching `tests/**` suite for .NET, `./Run-Tests.cmd` for
  TS), then re-run the publish script.
- **Credential/auth errors** (missing API key, `npm whoami` failure, scope
  access) → report to the user and stop; never work around credentials.
- **Transient push errors** → the scripts already retry; re-run once before
  reporting.

### Step 5: Offer the changelog update

Determine the published version first — NuGet pushes are NOT instantly
visible on NuGet.org, so `/changelog-update` must be told the version
explicitly instead of detecting it:

```powershell
dotnet nbgv get-version -v NuGetPackageVersion
```

(The TS publish output also prints `Published version X` — it uses the same
nbgv version.)

Then, unless Step 1 already pre-answered it, ask the user whether to run
`/changelog-update` for the published version. If yes (or pre-answered),
invoke the `changelog-update` skill passing that exact version as its
argument, and mention in its input which targets (NuGet, npm, or both) were
just published.
