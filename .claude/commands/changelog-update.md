---
allowed-tools: Read, Edit, Bash, Glob, Grep, WebFetch
description: Generate a new CHANGELOG.md entry from commits
argument-hint: [version-number] - e.g., "12.0.0"
---

# Changelog Entry Generator

Generate a new CHANGELOG.md entry based on commits since the last release.

## Instructions

### Step 1: Determine Version Number

If `$ARGUMENTS` is provided, use it as the version number. Otherwise:

1. **Check local build artifacts**:
   ```bash
   ls -la artifacts/nupkg/*.nupkg 2>/dev/null | head -5
   ```

2. **Check NuGet for the latest published version**:
   ```bash
   # Fetch latest version from NuGet API
   curl -s "https://api.nuget.org/v3-flatcontainer/actuallab.fusion/index.json" | jq -r '.versions[-1]'
   ```

3. **Check recent commits for version bumps**:
   ```bash
   git log --oneline -20 | grep -i "version"
   ```

4. Use the **greater** of artifacts/nupkg version vs NuGet version, or ask the user.

**Important**: Use the exact version number detected from artifacts or NuGet—do **not** invent a new version number by incrementing the detected one (e.g., if the highest detected version is `12.0.65`, don't create an entry for `12.0.66` unless you actually see `12.0.66` in artifacts or NuGet). If the detected version is newer than the last CHANGELOG entry, add a new entry for it. The entry always points to HEAD with format `<version>+<HEAD-hash>`.

### Step 2: Read Current CHANGELOG

Read `docs/CHANGELOG.md` to understand the format and find the last version's commit hash from the header - e.g., `## 11.4.7+3045fd2c`.

### Step 3: Fetch Commits Since Last Release

```bash
git log <last-hash>..HEAD --pretty=format:"%h %s"
```

### Step 4: Check for Breaking Changes

Breaking changes require special attention. Check these sources:

1. **Commits with breaking indicators**:
   - Prefix BREAKING: or contains "Breaking"
   - Commits with exclamation mark after type: feat!: or refactor!:

2. **Documentation changes** (`docs/` folder):
   - API changes documented in `PartAP*.md` files are **breaking** if the API signature changed
   - Check `git diff <last-hash>..HEAD -- docs/PartAP*.md` for API changes
   - Review changes to cheat sheets (`*-CS.md`) for removed/renamed APIs

3. **Sample changes** (`samples/` folder):
   - Check `git diff <last-hash>..HEAD -- samples/` for required migration patterns
   - If samples needed updates to work with new APIs, those are breaking changes

4. **Samples repository** (if available at `$AC_Project2Path` or `/proj/ActualLab.Fusion.Samples`):
   - Check if it's been upgraded to the current version
   - Look for migration commits that show breaking change adaptations
   ```bash
   # If samples repo is available
   git -C "$AC_Project2Path" log --oneline -20 2>/dev/null || \
   git -C /proj/ActualLab.Fusion.Samples log --oneline -20 2>/dev/null
   ```

**Rule**: If an API is documented in `docs/`, consider any change to its signature as a **breaking change**.

### Step 5: Categorize Commits

Map conventional commit prefixes to CHANGELOG sections:

| Prefix | CHANGELOG Section |
|--------|-------------------|
| `feat:` | Added |
| `fix:` | Fixed |
| `perf:` | Performance |
| `refactor:` | Changed |
| `docs:` | Documentation |
| `test:` | Tests |
| `chore:` | (usually skip unless significant) |
| BREAKING: / feat!: / fix!: etc. | Breaking Changes |

### Step 6: Generate the Entry

Format:
```markdown
## <version>+<short-hash>

Release date: <YYYY-MM-DD>

### Breaking Changes
- (if any - document migration path when possible)

### Added
- Feature descriptions

### Changed
- Significant changes

### Performance
- Performance improvements

### Documentation
- Doc updates (only major ones)

### Fixed
- Bug fixes

### Tests
- Test additions (only if significant)

### Infrastructure
- Build/tooling changes (only if significant)
```

### Step 7: Writing Guidelines

- **Focus on major changes** - not every commit needs an entry
- **Group related commits** - multiple commits for one feature = one entry
- **Skip trivial commits** - minor chores, formatting, typo fixes
- **User-facing language** - describe the impact, not the implementation
- **Start with verb** - "Added", "Fixed", "Improved", etc.
- **Include context** - mention affected components (RPC, Fusion, Blazor, etc.)
- **Omit empty sections** - don't include sections with no entries
- **Breaking changes need migration hints** - briefly explain what users need to change

### Step 8: Get HEAD Commit Hash

```bash
git rev-parse --short HEAD
```

### Step 9: Insert the Entry

Insert the new entry at the top of `docs/CHANGELOG.md` (after the header/intro, before the first `## X.Y.Z` section).

## Example Output

```markdown
## 12.0.0+3e71b6ef

Release date: 2025-01-27

### Breaking Changes
- Removed `WebSocketChannel` class - use `RpcWebSocketTransport` directly instead
- `RpcSerializationFormat` renamed `mempack5` to `mempack6` (update format strings)

### Added
- `mempack6` and `msgpack6` serialization format versions with improved performance
- `RpcStream.BatchSize` property for controlling stream batching behavior
- `UseTaskYieldInFrameComposer` option in `RpcWebSocketTransport` for frame optimization

### Changed
- Consolidated buffer size properties in `RpcWebSocketTransport`

### Performance
- Improved buffer renewal and reuse logic in RPC transport layer
- Optimized frame composition with configurable task yielding
- Switched to `UnboundedChannelOptions` for write channel to reduce contention

### Fixed
- .NET Standard 2.0 compatibility issues
- WebSocket message type handling in transport layer
- Buffer safety in `RpcCacheInfoCapture` argument data handling
```

## Your Task

Generate a CHANGELOG entry for version: $ARGUMENTS

Follow all steps above. If no version is specified, determine it from artifacts/NuGet as described.
