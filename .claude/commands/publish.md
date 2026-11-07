---
allowed-tools: Read, Edit, Bash, Glob, Grep, WebFetch, AskUserQuestion, Skill, mcp__voxt-robokitty__list_messages, mcp__voxt-robokitty__post_message
description: Publish NuGet (.NET) and npm (TS) packages via the Publish GitHub workflow, update the changelog, then announce the release
argument-hint: "[-post-only] [-test|-smoke-test|-no-test] [-changelog|-no-changelog] [-post|-no-post]"
---

# Publish

Publish ActualLab.Fusion packages to NuGet (.NET) and npm (TS), then announce
the release on Voxt. Every publish ships both: the `Publish` GitHub workflow
(`.github/workflows/Publish.yml`) runs the smoke tests, then pushes all NuGet and
npm packages with the same version.

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

`$ARGUMENTS` holds flags; **the default is `-test -changelog -post`**.

- `-post-only` → **publish nothing.** Announce the release that's already
  published (Step 7) and stop. Skip Steps 2–6 entirely; the version comes from
  the topmost `docs/CHANGELOG.md` entry. Use this to announce a release that was
  published earlier, or to retry an announcement that was skipped or failed.

Flags:
- `-test` (default) → the full local pre-publish test pass (Step 4), then the workflow's
  smoke tests (Step 5)
- `-smoke-test` → just the workflow's smoke tests: skip the local pass
- `-no-test` → no tests at all: skip both the local pass and the workflow's smoke tests
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

Then check the environment. The packages are built and pushed by the `Publish` GitHub
workflow (`.github/workflows/Publish.yml`), which authenticates to NuGet and npm via
trusted publishing — no API keys are involved locally. Locally you only need `pwsh`
and a `gh` CLI authenticated with the right to run workflows in `ActualLab/Fusion`
(`gh auth status`). `GH_TOKEN` is often another organization's fine-grained token that
gets 403 on starting workflows here, so `build/Publish.cmd` switches to `GH_TOKEN_FUSION`
when it's set. If `GH_TOKEN_FUSION` isn't set (`pwsh -c '[bool]$env:GH_TOKEN_FUSION'`),
STOP and tell the user to set it — a token owned by `ActualLab`, covering the Fusion repositories, with "Actions: Read and write".

### Step 3: Check there's something to publish

Find the latest published version (`npm view @actuallab/core version`, or the last entry of
`https://api.nuget.org/v3-flatcontainer/actuallab.core/index.json`), map it to its commit
(`dotnet nbgv get-commits <version>`), and list what changed since:
`git log <commit>..HEAD --oneline -- src/ ts/packages/ Directory.Packages.props Directory.Build.props version.json`.
If nothing shipping changed, **skip publishing**: report what was checked and stop.
Otherwise report the version→commit mapping and a one-line diff summary, then proceed.

### Step 4: Local pre-publish tests (skip with `-smoke-test` or `-no-test`)

The workflow runs the smoke tests (all TS tests plus the .NET tests marked with
`[Trait("Category", "Smoke")]`) before publishing anything, unless `-no-test` is given.
`-test` also adds the full local pass:

- **.NET**: run the parallel test suite via the `/test` skill (equivalently:
  `./Run-Tests.cmd` from the repository root — `all fast` mode). It must be green;
  flaky-but-passing-on-rerun tests (as classified by `/test`) don't block publishing.
- **TS**: from the `ts/` folder run `./Run-Lint.cmd`, then `./Run-Tests.cmd`
  (vitest). Both must pass.

If something fails: fix the errors, re-run the failed part, and repeat until green.
Don't skip or suppress failures. (With `-smoke-test` or `-no-test`, still run `ts/Run-Lint.cmd` —
it's fast and catches build breaks the workflow doesn't check.)

### Step 5: Publish

Run `build/Publish.cmd` once, adding `-no-test` when the skill got `-no-test`
(it sets the workflow's `skip-tests` input). It re-checks that `master` is clean and matches
`origin/master`, starts the `Publish` workflow on that commit, and waits for it
(`gh run watch`), printing the failed step's log on failure.

**The run waits for an approval.** Its `release` environment requires one of its reviewers
(`alexyakunin`, `alexis-kochetov`, `crui3er`, `frolyo`; set in the repository settings) to
approve it before the job starts. Right after starting it, tell the user to approve the run
on its page (the script prints nothing until the job moves, so give them the run URL from
`gh run list --workflow Publish.yml --limit 1`). Never approve it yourself.

The workflow:

1. runs the smoke tests (skipped with `-no-test`);
2. packs with `PUBLIC_BUILD=1`, gets a 1-hour NuGet key via `NuGet/login`,
   and pushes every package from `artifacts/nupkg/` to NuGet.org;
3. runs `ts/Publish.ps1`, which stamps the nbgv version into
   `packages/*/package.json`, builds, and publishes all `@actuallab/*` workspaces to npm.

Use a long Bash timeout (or run it in the background) — the workflow takes a while.

If a publish fails:

- **Test or build errors** → fix them, run the tests targeting the fixed
  code (e.g., `/test` for .NET, `ts/Run-Tests.cmd` for TS), commit and push the fix,
  then re-run the publish script.
- **Auth errors** (`NuGet/login` failure, 403 on push, npm `ENEEDAUTH`/`E404` on
  publish) → report to the user and stop; the trusted publishing policy on nuget.org /
  npmjs.com or the `NUGET_USER` secret needs fixing. Never work around credentials.
- **Transient push errors** → re-run once before reporting. NuGet pushes use
  `--skip-duplicate`, so a re-run skips packages that already went out; npm doesn't,
  so if NuGet succeeded and npm failed partway, ask the user before re-running.

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
argument, and mention in its input that both NuGet and npm were just published.

The entry has to be committed and pushed to `master` — Step 7 verifies it on the
live docs site, which builds from the remote.

Then put the entry into the GitHub release. The workflow's `release` job created
`v<version>` (on the published commit) with placeholder notes; replace them with the
entry's body (everything under its `##` header) followed by a
`More details: <changelog link>` line (the anchor is built as in Step 7):

```bash
GH_TOKEN="$GH_TOKEN_FUSION" gh release view "v<version>" --repo ActualLab/Fusion   # must exist
GH_TOKEN="$GH_TOKEN_FUSION" gh release edit "v<version>" --repo ActualLab/Fusion --notes-file tmp/release-notes.md
```

If the release is missing (the `release` job failed), report it to the user rather than
creating it from your machine: a tag created locally may point at a different commit.

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
format.

**Layout.** The established shape is a `#` heading with the changelog entry's own
title, a one-line summary, bold section labels over bullets, and the link last as
"More details:". Blank lines matter — the chat adds its own spacing around
headings and lists, so a blank line where it already spaces renders as a gap.
This exact skeleton is what the last release used, blank lines included:

```
# <version>+<hash> | npm: <npm-version>
<one-line summary: what kind of release it is, and who should upgrade>

**Fixed**
- <the headline item, in full>
<a follow-up paragraph attached directly to the bullet it elaborates>

**Breaking** (only if <the narrow condition under which it bites>)
- <before → after, concretely>
**Added**
- <the new API and what it's for>

More details: <changelog link>
```

So: no blank line after the `#` heading, none between a bullet and a paragraph
continuing it, and none between a bullet list and the label that follows it — but
keep one between a paragraph and the next label.

**Content.** Within that shape:

- Lead with what actually matters to a user deciding whether to upgrade — the
  headline fix or feature, and who is affected.
- Call out breaking changes explicitly, with the concrete migration ("rename X to
  Y", "override the method instead of the property").
- Use backticks for type and member names.
- Concrete numbers land better than adjectives — "returns in 44ms, the second one
  times out", "171 and 732 attempts were observed".
- Leave out infrastructure, docs, and tooling churn — it's release noise.
- Keep it short. A few paragraphs, not a recital of the changelog.

**4. Show the draft to the user and get approval before posting.** Posting to a
public release channel reaches real users and can't be quietly undone, so treat
it like the publish itself: present the exact text, ask whether to post it as-is
or adjust, and post only after an explicit yes. If anything in the draft rests on
a guess — an unverified claim about what's affected, a link you couldn't confirm —
say so alongside the draft instead of burying it.

**The draft must be on screen before any question about it.** Print the exact
text in a normal message, end that message with a plain "Post as-is, or adjust?",
and **end your turn** — the user's reply is the approval. Do NOT call
AskUserQuestion in the same response as the draft: the dialog opens before the
preceding text is visible, so the user is asked to approve something they can't
see. If you want a dialog anyway, it has to carry the full draft text itself.
