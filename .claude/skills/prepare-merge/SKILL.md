---
name: prepare-merge
description: |
  Use when a feature branch is complete and its history must be cleaned up
  before merging back to master — as the final step before merging a PR,
  or when the user says "prepare for merge", "squash the branch",
  "clean up branch history", or "/prepare-merge".
allowed-tools:
  - Bash
  - Read
  - Grep
  - Glob
  - AskUserQuestion
---

# /prepare-merge — Clean up a feature branch before merging to master

Rebase the current feature branch onto `origin/master`, regroup its commits into
a history worth keeping, and drop the working plans this branch added under
`docs/plans/` — so master history records the work, not the fix trail.

**How much to squash is the user's call, not yours.** The skill works out the
branch's real concerns and presents up to two plans — Perfect and Medium.
One combined question selects the plan and whether to push after verification.
Explicit arguments can supply either or both decisions upfront. There is no
default commit count and no implicit permission to push.

## User-invocable

When the user types `/prepare-merge`, run this skill.

## Arguments and approval

- `--plan perfect|medium` selects the corresponding commit-structure policy.
  The actual commits come from the branch's concerns, not a fixed count.
- `--push` authorizes pushing the verified result with `--force-with-lease`.
- `--no-push` leaves the prepared history local.

Examples:

```text
/prepare-merge
/prepare-merge --plan medium --push
/prepare-merge --plan perfect --no-push
```

Reject unknown options, missing or invalid plan values, conflicting plan values,
and `--push` combined with `--no-push` before changing the branch.

With no arguments, ask **one combined question** in step 4: which plan to use,
and whether to push it after verification. Accept answers such as "Medium, push"
or "Perfect, local only". Make clear that choosing push authorizes the verified
result without a second confirmation. If the answer omits either decision, ask
only for that missing decision; never interpret a plan choice alone as push consent.

If arguments or an explicit instruction in the current request already supply a
decision, do not ask for it again. With both decisions supplied, show the concrete
plan and doc deletions and proceed without an approval pause. With only one supplied,
ask only for the other. A single-concern branch needs no structure choice, but
still needs an explicit push/no-push decision if none was supplied.

Stop on failed verification or an ambiguous conflict. If the result would materially
differ from the approved policy or plan, explain the difference and get approval
before proceeding; do not silently substitute another plan or push a changed result.
These are exception paths, not routine final-confirmation prompts.

## Base branch

`master`, unless `branch.<name>.base` says otherwise:

```bash
BASE=$(git config --get "branch.$(git branch --show-current).base" || echo master)
```

Every `origin/master` below means `origin/$BASE`.

## Preconditions (abort and tell the user if violated)

- Current branch is a feature branch — not `master` or `release/*`.
- Working tree is clean (`git status --short` empty; untracked files under `tmp/` don't count).
- `git fetch origin "$BASE"` succeeded.

## Steps

### 1. Safety backup

```bash
git branch <branch-name>-bak HEAD
```

Never rewrite history without this. If `<branch-name>-bak` already exists
from a previous run, keep it as-is — it points at the true original, and
overwriting it with already-rewritten history would defeat the backup.

### 2. Rebase onto origin/master

`git rebase origin/master` (skip if already based on its tip). On conflicts,
resolve preserving both sides' intent and `GIT_EDITOR=true git rebase
--continue`; if a resolution is not obvious from the code, `git rebase
--abort` and ask the user. Interactive git (`git rebase -i` with an editor,
`git add -p`/`-i`) hangs in this environment — never use it.

Line endings: the repo uses `core.autocrlf=true` with `text=auto`, and some files
are stored with CRLF in the index. A conflict resolution or regrouping must not
flip a file's stored line endings — compare `git show <rev>:<file> | grep -c $'\r$'`
before and after when in doubt.

Then capture the parity baseline: `REBASED=$(git rev-parse HEAD)`.

### 3. Identify working plans to drop

```bash
git diff --diff-filter=A --name-only $(git merge-base HEAD origin/master) HEAD -- docs/plans/
```

Only files **added** by this branch are deleted — plans are working documents
that die when the feature ships. Pre-existing files under `docs/plans/`, including
ones this branch merely modified, stay untouched. Include the deletion list in
step 4's plan preview.

### 4. Plan the commit structure and authorize the outcome

Read `git log --oneline origin/master..HEAD` and the full diff
`git diff origin/master..HEAD`, and work out what genuinely distinct
concerns the branch holds — separate features, separate fixes,
infrastructure the feature merely builds on.

That list of concerns is the input to the plans below. Unless the user
already selected a policy with `--plan` or an explicit instruction, do not choose
a structure yourself: which trade-off is right depends on how the user wants
this branch to read in master history.

Original commit boundaries are not the answer: fix/review-comment commits
always fold into what they fix, and several commits on one concern fold
together. But **never merge two unrelated concerns just to get the count
down** — a smaller history is not the goal, an honest one is.

Split at whatever granularity gives the cleanest history, **including
within a single file**: a file touched by two concerns gets its hunks
split between them. `git add -p` hangs in this environment, so stage
hunks through a patch instead:

```bash
git diff <base> HEAD -- <file> > tmp/f.patch   # then trim it to the wanted hunks
git apply --cached tmp/f.patch
```

Without a preselected policy, offer **up to two plans**. With one, work out
and show that policy's concrete commit list without asking the user to pick again:

| Plan | What it does |
|---|---|
| **Perfect** | One commit per distinct concern. Nothing unrelated is ever squashed together. The most commits, and the most faithful history. **This can come out identical to the branch's existing history** — if the commits already map one-to-one onto the concerns, Perfect means "leave the splits alone", and say so rather than inventing a regrouping. |
| **Medium** | Each real feature keeps its own commit, but secondary work is consolidated — all the small fixes into one commit, all the docs into one, and so on. |

Offer only the plans that actually differ for this branch — a single-concern
branch collapses both into the same one commit, and then there is no structure
choice. A preselected policy remains valid even when it yields the same commits
as the other one. Present each plan as its list of commit messages, together
with step 3's doc deletions. Label Perfect as "unchanged" when it reproduces the
current history.

Collect any missing plan and push/no-push decisions in **one combined question**,
as described under Arguments and approval. For example: "Which plan, and should
I push after verification? Reply 'Medium, push' or 'Perfect, local only'."
When using `AskUserQuestion`, collect both decisions in the same call. Build the
selected plan, verify it, and honor that push decision without a final approval
prompt. If both decisions were already explicit, proceed after showing the preview.

Messages follow the convention of recent master history (`git log --oneline -30
origin/master`): an area prefix such as `Rpc:`, `Core:`, `TS:`, `Docs:` or
`type(scope):`, then a summary. When folding several commits loses important
detail, keep the summary line and add a short body naming what was folded in.
Pass messages with a body through `git commit -F tmp/msg.txt`, and end every
message with the attribution trailers the session requires.

### 5. Squash

If the chosen plan reproduces the branch's existing history **and** step 3
found no plans to drop, there is nothing to rewrite: skip to step 6, and
tell the user the history was already right. Otherwise the mixed reset
below rebuilds it — that is also how the working plans get stripped
out of history rather than deleted in a trailing commit.

```bash
git reset --mixed $(git merge-base HEAD origin/master)
rm <files from step 3>
# then per planned group:
git add <group paths> && git commit -m "Area: summary"
```

Every change must belong to exactly one group before you start — a file
split across groups is staged hunk-by-hunk via `git apply --cached`, as
in step 4. After the last commit `git status --short` must be empty
(apart from untracked `tmp/`).

### 6. Verify (mandatory before claiming done)

- `git branch --show-current` → still the feature branch. A `git rebase
  --onto` given a non-branch revision (like `HEAD~0`) rebases a detached
  HEAD and leaves the branch ref behind — later steps would then rewrite
  and push a stale tip. If detached, repoint: `git branch -f <branch>
  <verified-tip>` and check it out.
- `git status --short` → empty (apart from untracked `tmp/`).
- `git diff $REBASED HEAD` → shows *only* the step-3 plan deletions.
  Compare against the post-rebase tip, not the backup branch — the backup
  predates the rebase, so diffing it mixes in upstream changes.
- If step 2 had to resolve conflicts, build the solution (`dotnet build
  ActualLab.Fusion.CI.slnf` if it exists, else the `.sln`) before pushing.
- Show `git log --oneline origin/master..HEAD` to the user.

### 7. Honor the approved push decision

Report the final commit list and deleted plan files. Do not ask whether the history
is fine again: step 4 or the explicit arguments already authorized the outcome.

For `--no-push` or a local-only answer, stop after verification and state clearly
that the prepared branch is local and has **not** been pushed. Keep the local backup.

For `--push` or an explicit push answer, push only after step 6 passes and the
result matches the approved policy or plan:

```bash
git push --force-with-lease origin <branch-name>
```

**Never push a `-bak` branch to the remote.** The backup stays local: it
is a safety net for this machine, not something for origin to carry.
Afterwards verify the branch tracks its remote cleanly (`git status`
shows "up to date with origin/<branch>").

Report the pushed ref and remind the user to delete the local backup after
the merge lands: `git branch -D <branch-name>-bak`.

## Common mistakes

| Mistake | Fix |
|---|---|
| Rewriting with no backup ref | Step 1 first, always |
| Parity-diffing against the backup branch | Use `$REBASED` (post-rebase tip) |
| Deleting pre-existing `docs/plans/` files | Only `--diff-filter=A` files from this branch |
| `git rebase -i` / `git add -p` | They hang here; mixed-reset + regroup, and `git apply --cached` for hunks |
| Flipping a CRLF file to LF while regrouping | Check the stored line endings of touched files |
| One commit per original "meaningful" commit | Boundaries come from the concerns in the diff, not from the old history |
| Squashing unrelated concerns to hit a lower count | Only the plan the user picked decides how much gets folded |
| Choosing a structure without user direction | Offer Perfect / Medium unless a policy was already selected |
| Regrouping a history that was already correct | Perfect may well be "unchanged" — offer it as such |
| Asking separately for the plan and then final push approval | Collect both decisions once; verify and honor them without another prompt |
| Asking again despite `--plan medium --push` | Show the concrete plan, then execute without an approval pause |
| Treating a plan choice alone as push permission | Ask only for the missing push/no-push decision |
| Pushing a failed or materially changed result | Stop; fix verification or get approval for the changed plan |
| Pushing despite `--no-push` | Leave the verified history local and report it as unpushed |
| Pushing a `-bak` branch to origin | Never: backups stay local |
| Overwriting an existing `-bak` on a rerun | It holds the true original; leave it untouched |
