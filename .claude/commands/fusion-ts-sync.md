---
allowed-tools: Bash, Read, Write, Edit, Glob, Grep, Agent, AskUserQuestion
description: Synchronize Fusion TypeScript code between ActualLab.Fusion and ActualChat projects
argument-hint: [direction-or-instructions]
---

# Fusion TypeScript Sync

Synchronize the local copy of Fusion TypeScript source files between two repositories.

## Source Locations

**Fusion (canonical source):**
- Project root: `/proj/ActualLab.Fusion`
- Core package: `/proj/ActualLab.Fusion/ts/packages/core/src/`
- RPC package: `/proj/ActualLab.Fusion/ts/packages/rpc/src/`

**ActualChat (local copy):**
- Project root: `/proj/ActualChat`
- Core copy: `/proj/ActualChat/src/nodejs/src/actuallab-core/`
- RPC copy: `/proj/ActualChat/src/nodejs/src/actuallab-rpc/`

## Package Mapping

| Fusion package | Fusion path | ActualChat path |
|---|---|---|
| `@actuallab/core` | `ts/packages/core/src/` | `src/nodejs/src/actuallab-core/` |
| `@actuallab/rpc` | `ts/packages/rpc/src/` | `src/nodejs/src/actuallab-rpc/` |

**Only `core` and `rpc` packages are copied to ActualChat.** The other Fusion packages (`fusion`, `fusion-react`, `fusion-rpc`) are NOT present in ActualChat - ignore them.

## Known Differences

- **`rpc-client-stream-sender.ts`** exists ONLY in ActualChat's `actuallab-rpc/`. It is an ActualChat-specific addition. If it doesn't exist in Fusion, that's expected - don't flag it as missing unless the user asks.
- Import paths differ: ActualChat uses relative imports like `../actuallab-core/index.js` while Fusion uses package imports like `@actuallab/core`. This is structural and NOT a difference to sync - ignore import path style differences when comparing.
- Test files exist only in Fusion (`tests/` subdirectories) - these are not synced.

## Sync Procedure

### Step 1: Gather State

1. Get the current git branch for both repos:
   ```bash
   git -C /proj/ActualLab.Fusion branch --show-current
   git -C /proj/ActualChat branch --show-current
   ```

2. For each package (core, rpc), compare files between both sides. Use `diff` to find content differences, ignoring import path style differences:
   ```bash
   # For each matching file pair, diff ignoring whitespace
   diff -u "/proj/ActualLab.Fusion/ts/packages/core/src/FILE" "/proj/ActualChat/src/nodejs/src/actuallab-core/FILE"
   diff -u "/proj/ActualLab.Fusion/ts/packages/rpc/src/FILE" "/proj/ActualChat/src/nodejs/src/actuallab-rpc/FILE"
   ```

3. For files that differ, check recent git log on BOTH sides to determine where changes were made:
   ```bash
   git -C /proj/ActualLab.Fusion log --oneline -10 -- "ts/packages/core/src/FILE"
   git -C /proj/ActualChat log --oneline -10 -- "src/nodejs/src/actuallab-core/FILE"
   ```

4. Identify files that exist only on one side (excluding known exceptions like `rpc-client-stream-sender.ts`).

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
| rpc-client-stream-sender.ts | rpc | ActualChat only | (known, skip) |

### No changes needed:
X files are identical.
```

Then ask: **"Proceed with sync? (yes/no/modify)"**

Wait for the user's confirmation before making ANY file changes.

### Step 4: Execute Sync (only after confirmation)

For each file to sync, copy the content from source to destination:
- When copying FROM Fusion TO ActualChat: read the Fusion file, adjust import paths from package-style (`@actuallab/core`) to relative-style (`../actuallab-core/index.js`), write to ActualChat.
- When copying FROM ActualChat TO Fusion: read the ActualChat file, adjust import paths from relative-style to package-style, write to Fusion.

**Import path translation rules:**
- `@actuallab/core` ↔ `../actuallab-core/index.js`
- `@actuallab/rpc` ↔ `../actuallab-rpc/index.js` (if cross-package imports exist)
- Within same package: relative imports should stay relative on both sides.

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
