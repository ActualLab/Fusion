<!-- AUTO-GENERATED — DO NOT EDIT. Built by `ai update-md` from AGENTS-Source.md (this folder) + AGENTS-Suffix.md (AgentCli). To change anything below, edit the source file(s) and re-run `ai update-md`. -->

# Project-specific Rules for ActualLab.Fusion

**YOU MUST READ [CODING_STYLE.md](CODING_STYLE.md) before writing or
modifying any C# code.** It's not optional. This project
**deviates from standard .NET conventions** on several points (notably:
no `Async` suffix on async methods; no XML docs on members; mixed brace
style). Default instincts from elsewhere will produce code that gets
rejected. If you haven't opened that file yet in this session, stop and
read it now.

**You MUST NOT write a single comment, docstring, or XML doc** without
first reading [CODING_STYLE.md → "Regular comments, docstrings, XML
documentation comments"](CODING_STYLE.md#regular-comments-docstrings-xml-documentation-comments).
You have a strong tendency to over-comment and to restate what the code
already says; that section explains exactly when a comment is justified
and when it isn't. Re-read it any time you're tempted to add a `//` or `///`.

# Git workflow — don't branch unless asked

Commit your changes directly to the default branch (`master`). **You typically
should NOT create a feature branch in this repo unless the user explicitly asks
for one.** Small, self-contained changes (docs, fixes, tweaks) belong on
`master`; a needless branch only adds a merge step later.

# Type Catalog — Reuse Existing Abstractions (CRITICAL)

This codebase is mature. **Reusing what already exists is more important
than writing something new.** A new helper that duplicates an existing one
is a defect, not a feature. **Always look first.**

Use [`docs/api-index.md`](docs/api-index.md) to discover existing
abstractions before writing new code. For the complete list, see
[`docs/api-index-full.md`](docs/api-index-full.md).

## Planning rule (mandatory)

**Every implementation plan MUST include a "Reuse" section** with two parts:

1. **Existing abstractions to reuse.** Research first. List the concrete
   types/functions you intend to call from the indexes above. 
   If you cannot find a fit, say so explicitly — silence is not acceptable.

2. **Reusability of new components.** For every new component the plan
   introduces, ask: *is this likely useful elsewhere?* If yes, the plan
   **must list an option to put it in a shared project** instead of the
   feature-specific one:
   - **C#**: `ActualLab.Core`
   - **TypeScript**: `ts/actuallab-core`.

   The plan should compare the local-vs-shared placement and recommend
   one. Default to shared when in doubt — promoting later is harder than
   placing correctly the first time.

If the work is small enough that you skip a written plan, you still owe
yourself the "look first" step: search the indexes for keywords related
to what you're about to write.

# Fusion documentation & source MCP

This repo backs a public, read-only MCP server at
`https://fusion.actuallab.net/mcp` (see [`docs/mcp-server.md`](docs/mcp-server.md)).
When it's connected, prefer it over guessing: it can search and read the Fusion
**docs** (mental model, glossary, API index) and the Fusion **source** — find
files or declarations by regex and read exact line ranges, or ripgrep across
`src` / `samples` / `tests`. The individual tools are self-describing once the
MCP is enabled, so they are not enumerated here.

# Use cross-platform PowerShell

`pwsh` (cross-platform PowerShell) command is available on any OS you run, so use it.

Before starting any task, read AGENTS.md files in every directory starting from the current one and above, up to the root one (project directory).

# Execution policy after plan approval

Once a plan is approved and the open questions in it have been resolved,
**push it to completion without stopping for confirmation between steps.**
Don't ask permission to move from one pre-approved step to the next.
Don't pause to summarize "I'm about to do X" between pre-agreed phases.
Don't ask the user to choose when the choice has minimal impact.

You stop and ask only when **all** of these are true:

1. You hit a **real obstacle** you can't resolve from context alone.
2. The choice **likely obsoletes the plan or forces significant rework** —
   not "minor implementation detail," but "the path branches into two very
   different futures."
3. Your best guess at the right answer has a **non-trivial chance of being
   wrong in a way that's hard to revert**.

Concretely, do NOT ask when:
- The next step is a mechanical consequence of an earlier approved step.
- Two options exist and either is reversible in a few minutes.
- One option is clearly best (≥ ~80% probability) on the available evidence.
- You're already mid-plan and the next step is just "keep going."
- The build is broken between phases and the user already said that's fine.

When in doubt, **act**, then briefly note the choice in the result so the
user can correct course if needed. A short "I picked X because Y; flag if
you'd prefer Z" beats a question that stalls progress.

# Building

If a `*.CI.slnf` (solution filter) file exists in the project root, use it
instead of the main `*.sln` file for building. The CI solution filter
excludes projects that require additional workloads (like MAUI) that may
not be installed in your environment.

```bash
# Preferred — uses CI solution filter (excludes workload-heavy projects)
dotnet build <Project>.CI.slnf

# Only if you have all workloads installed (including maui-android, etc.)
dotnet build <Project>.sln
```

# Testing

## Debugging Test Failures

**Start with the simplest test**: If tests take too long, hang, or multiple tests fail, find the simplest failing test in the group and debug that one first. Once fixed, move on to larger/more complex tests.

**Isolate issues with small tests**: If a larger test fails and you have a reasonable guess why, write a small dedicated test that isolates the specific issue. This gives you faster iteration cycles. Keep these isolation tests in the codebase—they have value as regression tests.

## Running Single Test Cases from Theories

xUnit `[Theory]` tests with `[InlineData]` don't allow running a single test case in isolation. To debug a specific case:

1. Create a temporary `[Fact]` helper that calls the theory method with the specific arguments
2. Debug using this helper fact
3. **Remove the helper fact** after you've finished debugging—these are temporary scaffolding only

```csharp
// Temporary helper - DELETE after debugging
[Fact]
public void MyTheory_SpecificCase() => MyTheory("specificArg", 42);

[Theory]
[InlineData("case1", 1)]
[InlineData("specificArg", 42)]  // The case you're debugging
public void MyTheory(string arg1, int arg2) { /* ... */ }
```

## Timeouts

Choose reasonable timeouts based on expected execution time. If a test should complete in seconds, don't set a 5-minute timeout—use 30 seconds or less. This helps you iterate faster.

**Rule of thumb**: When working on a single test, you shouldn't wait more than 1 minute if you know it should run faster. Pick a timeout that matches your expectations.

## Logging

If you're missing information in test logs:

1. Use `Warning` level logging—it's more likely to appear in output
2. Worst case: use `Console.Error.WriteLine()` to ensure messages appear in test output

# Temporary Files

**Important:** Do not create temporary files in the project root. Use the `<projectRoot>/tmp` folder instead for any temporary files, test scripts, debug outputs, screenshots, etc. This keeps the project root clean and makes it easier to gitignore temporary artifacts.

If AC_OS environment variable is defined, you're started with the AgentCli launcher (ai.ps1),
so your actual OS is specified in this environment variable.

# AgentCli Launcher (ai.ps1)

You may be started via the `ai.ps1` launcher script. It can run any of the
following agents in a chosen environment:

- **Claude Code** (default) — `claude`
- **OpenAI Codex** — `codex`
- **xAI Grok** — `grok`
- **Codename Goose** — `goose` (block/goose)
- **OpenCode** — `opencode` (sst/opencode)

…in any of the following environments:

- **Docker** (default) — sandboxed Linux container
- **WSL** — Windows Subsystem for Linux
- **OS** — directly on the host operating system

When started via the launcher, environment variables are set to help you
understand your environment. Check these variables to determine where you're
running and how to access projects.

## Agent Selector

The agent is the optional first positional arg (or the `--agent:<name>`
option); if omitted, `claude` is used. `--agent:` accepts the full agent names
only (`claude`, `codex`, `grok`, `goose`, `opencode`). Entry points:

- `ai` — the launcher itself; Claude by default, or pick the agent explicitly.
- `ai-codex` / `ai-grok` / `ai-goose` / `ai-opencode` — one-agent shortcuts (`= ai --agent:<name>`).

```
ai                 → claude,   Docker (default)
ai codex           → codex,    Docker (positional form)
ai-codex           → codex,    Docker (= ai --agent:codex)
ai-grok os         → grok,     host OS
ai-goose           → goose,    Docker (= ai --agent:goose)
ai --agent:goose   → goose,    Docker (explicit)
ai-opencode        → opencode, Docker (= ai --agent:opencode)
ai opencode wsl    → opencode, WSL
ai wsl             → claude,   WSL
ai os              → claude,   host OS  (default agent)
ai codex --dry-run → codex,    Docker (dry run)
```

Inside the sandboxed Docker container, each agent is invoked in its
"skip-approvals" mode (`claude --dangerously-skip-permissions`, `codex
--full-auto`, `grok` as-is, `goose session` with `GOOSE_MODE=auto`,
`opencode --auto`). On the host OS no such flag/mode is added — the agent runs
in its normal interactive/approval mode.

## Installation

Run once after cloning AgentCli to make the `ai` / `ai-codex` / `ai-grok` /
`ai-goose` / `ai-opencode` commands available everywhere and build the Docker
image (which contains all five CLIs pre-installed):

```
./ai.ps1 install
```

What `install` does, by host OS:
- **Windows** — adds the AgentCli folder to the *user* `Path` environment
  variable so the entry-point `.cmd` files resolve in any new shell.
- **macOS** — adds `alias ai=…`, `alias ai-codex=…`, `alias ai-grok=…`,
  `alias ai-goose=…`, `alias ai-opencode=…` (pointing at the polyglot `.cmd`
  files) to `~/.zshrc` and `chmod +x`'s them.
- **Linux / WSL** — same as macOS, but the aliases go into `~/.bashrc`.

After the PATH/alias step, `install` also links AgentCli's shared
`.claude/{commands,skills}` into `~/.claude/{commands,skills}/team/` and
triggers a Docker build of the AgentCli image (`claude-agentcli`). Install is
idempotent — running it again only updates what's stale.

Re-open the shell (or `source ~/.zshrc` / `~/.bashrc`) before using `ai`.

To undo everything `install` did — unregister those entry points, remove the
`team` links, stop the AgentCli docker-compose stack, and remove the AgentCli Docker image:

```
./ai.ps1 uninstall
```

Uninstall leaves per-project Docker containers, generated `AGENTS.md` /
`CLAUDE.md` files, and worktrees alone.

## Shared docker-compose stack

AgentCli ships a small `docker-compose.yml` with side processes every CLI
session can reach — currently the two `chrome-devtools-mcp` services that
bridge stdio chrome-devtools-mcp to streamable HTTP on host ports `8765`
and `8766`. To start it explicitly:

```
ai compose-start
```

You almost never need to run that yourself. **Any** agent launch
(`ai`, `ai-codex`, `ai-goose`, `ai codex os`, `ai grok wsl`, …) auto-starts the stack
once per OS boot session. The check is a tiny marker file in the OS temp
dir that stores the boot timestamp; if it matches the current boot,
auto-start is a no-op. Reboot invalidates it, so `docker compose up -d`
runs again the first time after a reboot. Admin commands (`build`,
`install`, `compose-start`, `update-md`, `chrome`, `edge`, `audio`,
`wt`/`fwt`/`bwt`/`rwt`) and dry runs skip the auto-start.

## Environment Variables

| Variable | Description                                     |
|----------|-------------------------------------------------|
| `AC_OS` | Operating system/environment description        |
| `AC_ProjectRoot` | Root directory containing all projects (`/proj` in Docker) |
| `AC_ProjectPath` | Full path to current project (or worktree)      |
| `AC_Worktree` | Worktree suffix (empty if not in a worktree)    |

If AC_OS has no value, you're started directly, so none of this is in effect.

## Detecting Your Environment

Check `AC_OS` to determine where you're running:
- `Linux in Docker` - Running in a Docker container (sandboxed)
- `Linux on WSL` - Running in Windows Subsystem for Linux
- `Windows` - Running directly on Windows
- `Linux` - Running directly on Linux
- `macOS` - Running directly on macOS

## Docker Environment

When running in Docker (`AC_OS` = `Linux in Docker`), the following tools are available:

| Category | Tools |
|----------|-------|
| **AI CLIs** | Claude Code, OpenAI Codex, xAI Grok, Codename Goose, OpenCode |
| **.NET** | .NET 10 SDK, .NET 9 SDK, wasm-tools workload |
| **Node.js** | Node.js 20, npm |
| **Shell** | Zsh (default), Bash, PowerShell (`pwsh`) |
| **Search** | ripgrep (`rg`), fd-find (`fdfind`), fzf |
| **Git** | git, gh (GitHub CLI), git-delta (nicer diffs) |
| **Editors** | vim, nano |
| **Python** | Python 3, matplotlib, seaborn, plotly, pandas, numpy, pillow |
| **Cloud** | gcloud CLI (Google Cloud), with host's gcloud config mounted read-only |
| **Testing** | Playwright with Chromium pre-installed |
| **Audio** | PulseAudio client, ALSA utils, SoX (for voice mode) |
| **Other** | jq, curl, wget, imagemagick, sudo |

When running in Docker, `/proj/<CurrentProject>/artifacts` path is mapped to `artifacts/claude-docker/` path in the OS's file system to avoid permission conflicts with the host.

**Host service connectivity**: The Docker container uses `--network host` mode. On **native Linux** this makes `localhost` inside the container refer to the host directly, so you reach host services (Redis, PostgreSQL, NATS, etc.) at `localhost:port`. On **Windows**, Docker Desktop's engine runs inside WSL2, so host reachability depends on WSL networking mode: with **mirrored** mode (`.wslconfig` → `networkingMode=mirrored`) the WSL2 backend shares the Windows loopback, so `--network host` reaches the host's `localhost:port` directly (host service may stay bound to `127.0.0.1`); in the default **NAT** mode it does not, and you'd need `host.docker.internal:port`. On **macOS**, `--network host` attaches to the Linux VM (not the host), so host services are reached via `host.docker.internal:port` (the service must bind `0.0.0.0`); requires Docker Desktop 4.34+ (Sept 2024).

**macOS / Apple Silicon**: The Docker image supports both amd64 and arm64 architectures. `ai.cmd` (and the `ai-codex.cmd` / `ai-grok.cmd` / `ai-goose.cmd` / `ai-opencode.cmd` shortcuts) are polyglot scripts that work on both Windows and macOS/Linux.

**Goose config**: When you launch the `goose` agent, the launcher passes your host goose config to the sandboxed/WSL goose so its provider setup (e.g. a local LM Studio endpoint) carries over. In Docker the host goose config dir (`%APPDATA%\Block\goose\config` on Windows, `~/.config/goose` elsewhere) is bind-mounted read-only to `/home/claude/.config/goose`; in WSL the `config.yaml` is copied into the WSL user's `~/.config/goose/`. A `localhost:1234` LM Studio endpoint in that config works unchanged: on native-Linux Docker and on **Windows with WSL mirrored networking** `--network host` already reaches the host's `localhost:1234` directly; on **macOS** the launcher starts an in-container `socat` forwarder so `localhost:1234` reaches `host.docker.internal:1234` (override the port with `AC_LMSTUDIO_PORT`; LM Studio must serve on `0.0.0.0`). Windows requires WSL mirrored mode (`.wslconfig` → `networkingMode=mirrored`); the socat proxy is intentionally **not** used there because it would hijack `:1234` on the shared loopback and loop back on itself.

**OpenCode config**: When you launch the `opencode` agent, the launcher passes your host OpenCode config (`~/.config/opencode/opencode.json(c)`) to the sandboxed/WSL opencode so its provider setup carries over. In Docker the host `~/.config/opencode` dir is bind-mounted read-only to `/home/claude/.config/opencode`; in WSL the `opencode.json(c)` is copied into the WSL user's `~/.config/opencode/`. Just like Goose, a `localhost:1234` LM Studio endpoint in that config is reachable via Windows WSL mirrored networking (or the macOS `socat` forwarder) — see the Goose note above. In the sandbox opencode runs with `--auto` (auto-approve).

**Propagated environment variables**: The following environment variables are automatically propagated from the host to the Docker container:
- Variables containing `__` in their names (e.g., `ChatSettings__OpenAIApiKey` for .NET configuration)
- `AC_GITHUB_TOKEN` - GitHub authentication token (AC_ prefix to avoid conflicts with gh CLI)
- `NPM_READ_TOKEN` - NPM registry read token
- `GOOGLE_CLOUD_PROJECT` - Google Cloud project ID
- `ActualChat_*` - Any variables prefixed with `ActualChat_`
- `ActualLab_*` - Any variables prefixed with `ActualLab_`
- `Claude_*` - Any variables prefixed with `Claude_`

**Google Cloud credentials**: The `~/.gcp` folder is mounted read-only to `/home/claude/.gcp`. If `GOOGLE_APPLICATION_CREDENTIALS` is set on the host, it's automatically remapped to `/home/claude/.gcp/key.json` inside the container.

**Container reuse**: By default, `c` reuses an existing Docker container for the current worktree (matched by the `worktree` label). If multiple containers exist, you'll be prompted to select one. Use `--new` to force creating a fresh container instead.

**Isolated mode**: Set `AC_CLAUDE_ISOLATE=true` (or `1`) to run with an isolated `.claude.json` config file. When enabled, the launcher copies `.claude.json` to `artifacts/claude-docker/.claude-{timestamp}.json` and mounts that copy instead of the original. Changes made inside the container are not synced back to the host's `.claude.json`. This is useful for parallel Claude instances or testing without affecting the main config.

## Browser Automation and Chrome Debugging

The user starts Chrome with remote debugging via `ai chrome` command (port 9222). On Windows, this also creates a firewall rule to allow connections from WSL/Docker.

**chrome-devtools MCP (preferred over Playwright)**: AgentCli's `docker-compose.yml` ships two `chrome-devtools` MCP services (`chrome-devtools-mcp-1` → host `8765`, `chrome-devtools-mcp-2` → host `8766`), each bridging stdio `chrome-devtools-mcp` to streamable HTTP via `supergateway`. They target the host Chrome ports `CHROME_DEBUG_PORT_1` (default `9222`) and `CHROME_DEBUG_PORT_2` (default `9223`) respectively, and recycle themselves when host Chrome flaps. When the matching MCP server entries are wired up in `.mcp.json` (look for `mcp__chrome{1,2}__*` tools — the older `mcp__chrome-devtools-{1,2}__*` names still resolve too), prefer them over Playwright — and pair them with the `/debug-ui` and `/server-loop` skills if those are available too.

**Playwright**: Playwright and Chromium are also pre-installed in the Docker image. Use Playwright when you need to write automated test scripts or when the chrome-devtools MCP is not available. When the user asks you to "use host Chrome", connect Playwright to Chrome on the host:

```typescript
import { chromium } from 'playwright';

// Connect to host Chrome on standard debug port
const browser = await chromium.connectOverCDP('http://localhost:9222');
const page = await browser.newPage();
await page.goto('https://example.com');
// ... user sees this in their Chrome window
```

Since Docker uses `--network host`, `localhost:9222` reaches the host's Chrome directly.

**Docker host IP resolution**: If `localhost` doesn't work (e.g., it resolves to `::1` IPv6 while Chrome listens on IPv4 only), resolve the host IP explicitly:

```bash
getent ahosts host.docker.internal | awk 'NR==1{print $1}'
```

Then use the resulting IP (e.g., `http://192.168.65.254:9222`) instead of `localhost`.

## Accessing Sibling Projects

`AC_ProjectRoot` always points to the directory that contains AgentCli — by
default, the folder one level above the AgentCli repo itself (override with
the `AC_ProjectRoot` env var). In Docker it is mounted as `/proj`, so sibling
projects sitting next to AgentCli are accessible at `/proj/<name>`.

| Environment | AC_ProjectRoot | Example sibling project |
|-------------|----------------|-------------------------|
| Docker | `/proj` | `/proj/ActualLab.Fusion` |
| WSL | `/mnt/d/Projects` | `/mnt/d/Projects/ActualLab.Fusion` |
| Windows | `D:\Projects` | `D:\Projects\ActualLab.Fusion` |
| macOS | `~/Projects` | `~/Projects/ActualLab.Fusion` |

## Launching from any folder

The launcher does **not** require the current folder to be a sibling of
AgentCli (or even a git repo). You can `c` from anywhere — your home dir,
a one-off scratch folder, a project under another drive — and it will work.

The behavior splits along the Docker vs. OS/WSL line:

- **OS / WSL** — `AC_ProjectPath` is just the real, untranslated path to the
  current folder. Nothing fancy.
- **Docker** — the container's filesystem doesn't have access to arbitrary
  host paths, so when the launch folder is **not** under `AC_ProjectRoot`,
  the launcher:
  1. Sanitizes the full host path into a single segment (path separators and
     the drive colon all become single underscores: `C:\Users\Alex\foo` →
     `C_Users_Alex_foo`).
  2. Adds an extra Docker mount that exposes that folder under
     `/proj/<sanitized>`.
  3. Sets `AC_ProjectPath=/proj/<sanitized>` so paths inside the container
     line up. Sibling projects under `AC_ProjectRoot` are still reachable at
     `/proj/<sibling>` as usual.

The Docker image (`claude-<AgentCli folder>`) is **shared by every project** —
there is no per-project `Dockerfile` anymore. The image is built by
`ai install` or `ai build` (always against AgentCli's own `Dockerfile`).

## Editing AGENTS.md / CLAUDE.md

**Do not edit `AGENTS.md` or `CLAUDE.md` directly — they are auto-generated.**
Both files are byte-identical and produced by `ai update-md`, which
concatenates:

1. `AGENTS-Source.md` from the project where `ai.ps1` was launched (the local,
   project-specific part — edit this for anything project-specific).
2. `AGENTS-Suffix.md` from the AgentCli repo (the shared boilerplate —
   edit this only if the change should apply to *every* project).

To change anything in `AGENTS.md` / `CLAUDE.md`, edit `AGENTS-Source.md`
(or `AGENTS-Suffix.md`) and then run `ai update-md` to regenerate.

Run after editing either part:

```
./ai.ps1 update-md
```

## Worktree Support

The launcher supports git worktrees, detected automatically via git.

**Auto-detection**: If you're in a folder like `ActualLab.Fusion-feature1`, the launcher detects it as a worktree of `ActualLab.Fusion` and sets:
- `AC_Worktree` = `feature1`
- `AC_ProjectPath` = path to the worktree folder

**Creating worktrees**: Use the `wt` command to create and switch to a worktree:
```
ai wt feature1   # Creates ActualLab.Fusion-feature1 if it doesn't exist and runs there
```

The worktree is created using `git worktree add` from the main project directory.

# Fusion documentation and source code

Several projects here build on **ActualLab.Fusion** — an end-to-end
reactivity framework for .NET and TypeScript (automatic caching, dependency
tracking, precise invalidation, RPC, and reactive client state). When you
work with Fusion, two complementary resources may be available; use them.

## `fusion-docs` MCP

A documentation + source MCP server named **`fusion-docs`** may be wired up
(look for `mcp__fusion-docs__*` tools). When present, it's the fastest way to
get Fusion right — prefer it over guessing. It can search and read both the
Fusion **documentation** (mental model, glossary, API index) and the Fusion
**source** — find files or declarations by regex and read exact line ranges, or
ripgrep across `src` / `samples` / `tests`. Start with the `intro` tool if
you're unsure; the individual tools are self-describing once connected, so use
the tool list (not this summary) for exact arguments.

It's backed by the docs and source in the **ActualLab.Fusion** repository, so it
stays in sync with the code. The same server hosts the docs site and the MCP at
`https://fusion.actuallab.net` (endpoint: `/mcp`).

## ActualLab.Fusion source, samples, and tests

The **ActualLab.Fusion** project — a clone of the Fusion GitHub repository —
is available as a sibling project (e.g. `/proj/ActualLab.Fusion` in Docker,
`D:\Projects\ActualLab.Fusion` on Windows; see **Accessing Sibling
Projects**). It contains the full framework source, plus **samples** and
**tests** that demonstrate correct, idiomatic Fusion usage. When the docs
don't fully answer a question, read the actual source, the sample apps, or
the tests to see how a feature is meant to be used. Fusion samples may also
live in a separate **ActualLab.Fusion.Samples** sibling project.
