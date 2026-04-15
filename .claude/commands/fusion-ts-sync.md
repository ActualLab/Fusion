---
allowed-tools: Bash, Read, Write, Edit, Glob, Grep, Agent, AskUserQuestion
description: Synchronize Fusion TypeScript code between ActualLab.Fusion and ActualChat projects
argument-hint: [direction-or-instructions]
---

# Fusion TypeScript Sync

Synchronize the local copy of Fusion TypeScript source files between two repositories.

## Packages to Sync

Two packages are synced: `@actuallab/core` and `@actuallab/rpc`. These are the only Fusion TS packages that ActualChat consumes. The other Fusion packages (`fusion`, `fusion-react`, `fusion-rpc`) are NOT present in ActualChat — ignore them.

| Fusion package | Fusion path | ActualChat path |
|---|---|---|
| `@actuallab/core` | `ts/packages/core/src/` | `src/nodejs/src/actuallab-core/` |
| `@actuallab/rpc` | `ts/packages/rpc/src/` | `src/nodejs/src/actuallab-rpc/` |

**Source locations:**
- Fusion project root: `/proj/ActualLab.Fusion`
- ActualChat project root: `/proj/ActualChat`

## Expected Differences

The two copies should be **identical** except for one structural difference:

- **Cross-package import paths**: RPC files that import from core use different path styles:
  - Fusion: `from '@actuallab/core'` (npm package import)
  - ActualChat: `from '../actuallab-core/index.js'` (relative import)

This is the ONLY expected difference. When comparing files, ignore lines that differ solely in this import path style. When syncing, translate between the two styles.

**Files must match 1:1** — the same set of files should exist on both sides. If a file exists on one side but not the other, it should be synced (created) or flagged.

**Test files** (`tests/` subdirectories) exist only in Fusion and are not synced.

## Sync Procedure

### Step 1: Gather State

1. Get the current git branch for both repos:
   ```bash
   git -C /proj/ActualLab.Fusion branch --show-current
   git -C /proj/ActualChat branch --show-current
   ```

2. For each package (core, rpc), compare files between both sides. Use `diff` to find content differences, ignoring import path style differences:
   ```bash
   diff -u "/proj/ActualLab.Fusion/ts/packages/core/src/FILE" "/proj/ActualChat/src/nodejs/src/actuallab-core/FILE"
   diff -u "/proj/ActualLab.Fusion/ts/packages/rpc/src/FILE" "/proj/ActualChat/src/nodejs/src/actuallab-rpc/FILE"
   ```

3. For files that differ (beyond the expected import path difference), check recent git log on BOTH sides to determine where changes were made:
   ```bash
   git -C /proj/ActualLab.Fusion log --oneline -10 -- "ts/packages/core/src/FILE"
   git -C /proj/ActualChat log --oneline -10 -- "src/nodejs/src/actuallab-core/FILE"
   ```

4. Identify files that exist only on one side.

### Step 2: Determine Sync Direction

For each differing file, determine the sync direction:

- **If `$ARGUMENTS` specifies a direction** (e.g., "from actchat", "from fusion", "fusion unchanged", "actchat unchanged"):
  - "from actchat" / "fusion unchanged" / "fusion wasn't changed" → copy all changes FROM ActualChat TO Fusion
  - "from fusion" / "actchat unchanged" / "actchat wasn't changed" → copy all changes FROM Fusion TO ActualChat
  - Apply this as the default direction for all files unless there's a conflict.

- **If no direction specified**, use git history to determine:
  - If only one side has commits touching the file → sync from that side
  - If both sides have changes → flag as conflict, ask the user

### Step 3: Print Summary and Ask for Confirmation

**IMPORTANT: Do NOT make any changes before getting user confirmation.**

Print a summary in this format:

```
## Fusion TS Sync Summary

**Fusion branch:** `master`
**ActualChat branch:** `dev`

### Files to sync:

| File | Package | Direction | Reason |
|------|---------|-----------|--------|
| compute-foo.ts | core | ActualChat → Fusion | Changed in ActualChat (abc1234) |
| rpc-peer.ts | rpc | Fusion → ActualChat | Changed in Fusion (def5678) |

### Files with conflicts:
| File | Package | Fusion commit | ActualChat commit |
|------|---------|---------------|-------------------|
| events.ts | core | aaa1111 (2d ago) | bbb2222 (1d ago) |

### Files only in one side:
| File | Package | Location | Action |
|------|---------|----------|--------|
| new-file.ts | rpc | Fusion only | Create in ActualChat |

### No changes needed:
X files are identical (modulo import paths).
```

Then ask: **"Proceed with sync? (yes/no/modify)"**

Wait for the user's confirmation before making ANY file changes.

### Step 4: Execute Sync (only after confirmation)

For each file to sync, copy the source file to the destination, then fix import paths:

```bash
# Fusion → ActualChat: copy then fix imports
cp "/proj/ActualLab.Fusion/ts/packages/rpc/src/FILE" "/proj/ActualChat/src/nodejs/src/actuallab-rpc/FILE"
sed -i "s|from '@actuallab/core'|from '../actuallab-core/index.js'|g" "/proj/ActualChat/src/nodejs/src/actuallab-rpc/FILE"

# ActualChat → Fusion: copy then fix imports
cp "/proj/ActualChat/src/nodejs/src/actuallab-rpc/FILE" "/proj/ActualLab.Fusion/ts/packages/rpc/src/FILE"
sed -i "s|from '../actuallab-core/index.js'|from '@actuallab/core'|g" "/proj/ActualLab.Fusion/ts/packages/rpc/src/FILE"
```

Core files have no cross-package imports, so no import path fixup is needed for them.

After syncing, verify with a final diff that files match (modulo import paths).

### Step 5: Report

Print which files were synced and in which direction. Do NOT commit changes - leave that to the user.

## Additional Arguments ($ARGUMENTS)

The user may provide additional instructions: $ARGUMENTS

Interpret these as overrides or clarifications for the sync process. Common patterns:
- Direction hints: "from fusion", "from actchat", "fusion wasn't changed", etc.
- Specific files: "only sync rpc-peer.ts"
- Skip files: "skip events.ts"
- Any other instructions take precedence over the default behavior.
