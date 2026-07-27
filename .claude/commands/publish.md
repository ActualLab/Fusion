---
allowed-tools: Read, Edit, Bash, Glob, Grep, WebFetch, AskUserQuestion, Skill, mcp__voxt-robokitty__list_messages, mcp__voxt-robokitty__post_message
description: Publish NuGet (.NET) and/or npm (TS) packages; auto-detects what changed, tests via /test, updates the changelog, then announces the release
argument-hint: "[.net|ts|both|auto|-post-only] [-test|-no-test] [-changelog|-no-changelog] [-post|-no-post]"
---

# Publish

Publish ActualLab.Fusion packages to NuGet (.NET) and/or npm (TS), then announce
the release on Voxt.

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

`$ARGUMENTS` holds a target and flags; **the default is `auto -test -changelog -post`**.

Target:
- `.net`, `dotnet`, or `net` → publish .NET (NuGet) only
- `ts` → publish TypeScript (npm) only
- `both`, `all` → publish both (order: .NET first, then TS)
- `auto` (or no target) → detect what needs publishing (Step 3)
- `-post-only` → **publish nothing.** Announce the release that's already
  published (Step 7) and stop. Skip Steps 2–6 entirely; the version comes from
  the topmost `docs/CHANGELOG.md` entry. Use this to announce a release that was
  published earlier, or to retry an announcement that was skipped or failed.

Flags:
- `-test` (default) / `-no-test` → run or skip the pre-publish test pass (Step 4)
- `-changelog` (default) / `-no-changelog` → run or skip `/changelog-update` after
  publishing (Step 6)
- `-post` (default) / `-no-post` → announce the release on Voxt (Step 7), or skip
  it and finish right after the changelog

`-no-changelog` implies `-no-post`: the announcement links to a changelog entry,
so there's nothing to announce without one. If both `-changelog` and `-no-post`
are given, that's fine — write the entry, skip the announcement.

### Step 2: Check the branch and the environment

*(Skipped entirely by `-post-only`, which jumps straight to Step 7.)*

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

The entry has to be committed and pushed to `master` — Step 7 verifies it on the
live docs site, which builds from the remote.

### Step 7: Announce the release (skip with `-no-post`)

Announce in Fusion's "🎉Releases" chat on Voxt — chat id
`s-1KCdcYy9z2-uJVPKZsbEo` — using `mcp__voxt-robokitty__post_message`.

**1. Build the changelog anchor.** The site turns an entry header into an anchor
by lowercasing it and replacing every run of non-alphanumeric characters with a
single `-`, prefixed by `_`:

```
## 14.1.62+ab9673b6 | npm: 14.1.5
  → https://fusion.actuallab.net/CHANGELOG#_14-1-62-ab9673b6-npm-14-1-5
```

**2. Confirm the entry is live before announcing anything.** The docs site
rebuilds from `master` and lags a push by a few minutes, so a link posted right
after the push can point at a changelog that doesn't have the entry yet.
WebFetch `https://fusion.actuallab.net/CHANGELOG` and confirm the new version's
header text is actually present.

- Not there yet → wait a bit and re-check, a few times.
- Still missing after several tries → **STOP and ask the user** whether to keep
  waiting or skip the announcement. Never announce a link you haven't seen
  resolve; a wrong link in a release channel is exactly the kind of thing this
  skill exists to prevent.

**3. Match the channel's voice.** Read back up to **100 messages**
(`mcp__voxt-robokitty__list_messages` on the same chat id) and study the past
release announcements — the ones from **Alex Yakunin** and **RoboKitty**, which
are the model to imitate. A short tail isn't enough: releases are spaced out, so
20 messages can be all follow-up discussion and no announcement at all. Read
enough of them to see the recurring shape, then follow it instead of inventing a
format. As of this writing that shape is the changelog link on its own first
line, then a few short plain-spoken paragraphs. Specifically:

- Lead with what actually matters to a user deciding whether to upgrade — the
  headline fix or feature, and who is affected.
- Call out breaking changes explicitly, with the concrete migration ("rename X to
  Y", "override the method instead of the property").
- Mention when a target was skipped, e.g. "NuGet-only release (npm stays at
  `14.1.5`)".
- Use backticks for type and member names.
- Leave out infrastructure, docs, and tooling churn — it's release noise.
- Keep it short. A few paragraphs, not a recital of the changelog.

**4. Show the draft to the user and get approval before posting.** Posting to a
public release channel reaches real users and can't be quietly undone, so treat
it like the publish itself: present the exact text, ask (AskUserQuestion) whether
to post it as-is or adjust, and post only after an explicit yes. If anything in
the draft rests on a guess — an unverified claim about what's affected, a link
you couldn't confirm — say so alongside the draft instead of burying it.
