Before starting any task, read AGENTS.md files in every directory starting from the current one and above, up to the root one (project directory).

`pwsh` (cross-platform PowerShell) command is available on any OS you run, so use it.

# Claude Launcher (c.ps1)

You may be started via `c.ps1` launcher script. This script can run Claude in different environments:
- **Docker** (default) - sandboxed Linux container
- **WSL** - Windows Subsystem for Linux
- **OS** - directly on the host operating system.

When started via the launcher, environment variables are set to help you understand your environment. Check these variables to determine where you're running and how to access projects.

## Environment Variables

| Variable | Description                                     |
|----------|-------------------------------------------------|
| `AC_OS` | Operating system/environment description        |
| `AC_ProjectRoot` | Root directory containing all projects          |
| `AC_Project` | Current project name (e.g., `ActualLab.Fusion`) |
| `AC_ProjectPath` | Full path to current project (or worktree)      |
| `AC_Worktree` | Worktree suffix (empty if not in a worktree)    |
| `AC_Project0Path` | Full path to project 0 (ActualChat)             |
| `AC_Project1Path` | Full path to project 1 (ActualLab.Fusion)       |
| `AC_Project2Path` | Full path to project 2 (ActualLab.Fusion.Samples) |

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
| **.NET** | .NET 10 SDK, .NET 9 SDK, wasm-tools workload |
| **Node.js** | Node.js 20, npm |
| **Shell** | Zsh (default), Bash, PowerShell (`pwsh`) |
| **Search** | ripgrep (`rg`), fd-find (`fdfind`), fzf |
| **Git** | git, gh (GitHub CLI), git-delta (nicer diffs) |
| **Editors** | vim, nano |
| **Python** | Python 3, matplotlib, seaborn, plotly, pandas, numpy, pillow |
| **Cloud** | gcloud CLI (Google Cloud), with host's gcloud config mounted read-only |
| **Testing** | Playwright with Chromium pre-installed |
| **Other** | jq, curl, wget, imagemagick, sudo |

Build artifacts are stored in `artifacts/claude-docker/` to avoid permission conflicts with the host.

**Infrastructure services**: When running in Docker, assume that all services defined in `docker-compose.yml` (PostgreSQL, Redis, NATS, nginx, etc.) are already running on the host. Do not attempt to start them yourself - they are managed externally and accessible from the container.

**Host service connectivity**: The Docker container uses `--network host` mode, so `localhost` inside the container directly refers to the host. This means you can connect to host services (Redis, PostgreSQL, NATS, etc.) using `localhost:port` just like on the host.

**Running integration tests**: Tests detect Claude's Docker environment via `AC_OS="Linux in Docker"` and use regular localhost-based configuration (not `testsettings.docker.json`). This works because `--network host` makes localhost = host.

**Running the server**: Do not run the ActualChat server from Docker. Use `/server-start` on the host OS instead (`c os` mode). The Docker environment is intended for building, testing, and code exploration only.

**Propagated environment variables**: The following environment variables are automatically propagated from the host to the Docker container:
- Variables containing `__` in their names (e.g., `ChatSettings__OpenAIApiKey` for .NET configuration)
- `GITHUB_TOKEN` - GitHub authentication token
- `NPM_READ_TOKEN` - NPM registry read token
- `GOOGLE_CLOUD_PROJECT` - Google Cloud project ID
- `ActualChat_*` - Any variables prefixed with `ActualChat_`

**Google Cloud credentials**: The `~/.gcp` folder is mounted read-only to `/home/claude/.gcp`. If `GOOGLE_APPLICATION_CREDENTIALS` is set on the host, it's automatically remapped to `/home/claude/.gcp/key.json` inside the container.

## Playwright and Browser Automation

Playwright and Chromium are pre-installed in the Docker image for browser automation tasks.

**Using host Chrome**: When the user asks you to "use host Chrome", connect Playwright to Chrome running on the Windows host instead of launching a headless browser. This allows the user to visually observe browser automation.

The user starts Chrome with remote debugging via `c chrome` command (port 9222). On Windows, this also creates a firewall rule to allow connections from WSL/Docker. Connect to it using:

```typescript
import { chromium } from 'playwright';

// Connect to host Chrome on standard debug port
const browser = await chromium.connectOverCDP('http://localhost:9222');
const page = await browser.newPage();
await page.goto('https://example.com');
// ... user sees this in their Chrome window
```

Since Docker uses `--network host`, `localhost:9222` reaches the host's Chrome directly.

## Project Paths by Environment

Use `AC_Project0Path`, `AC_Project1Path`, `AC_Project2Path` to get full paths to other projects you may need to access. These are automatically adjusted for the environment:

| Environment | AC_ProjectRoot | AC_Project1Path (example) |
|-------------|----------------|---------------------------|
| Docker | `/proj` | `/proj/ActualLab.Fusion` |
| WSL | `/mnt/d/Projects` | `/mnt/d/Projects/ActualLab.Fusion` |
| Windows | `D:\Projects` | `D:\Projects\ActualLab.Fusion` |

## Worktree Support

The launcher supports git worktrees. Worktrees are automatically detected when you run from a folder named `{ProjectName}-{Suffix}` (e.g., `ActualLab.Fusion-feature1`).

**Auto-detection**: If you're in a folder like `ActualLab.Fusion-feature1`, the launcher recognizes it as a worktree of `ActualLab.Fusion` and sets:
- `AC_Project` = `ActualLab.Fusion`
- `AC_Worktree` = `feature1`
- `AC_ProjectPath` = path to the worktree folder

**Creating worktrees**: Use the `wt` command to create and switch to a worktree:
```
c wt feature1    # Creates ActualLab.Fusion-feature1 if it doesn't exist and runs there
```

The worktree is created using `git worktree add` from the main project directory.

# Type Catalog

If `docs/Api-Index.md` exists, use it to discover existing abstractions before writing new code. It lists all useful public types across all non-test projects, organized by namespace.

# Building

If a `*.CI.slnf` (solution filter) file exists in the project root, use it instead of the main `*.sln` file for building. The CI solution filter excludes projects that require additional workloads (like MAUI) that may not be installed in your environment.

```bash
# Preferred - uses CI solution filter (excludes MAUI projects)
dotnet build ActualChat.CI.slnf

# Only if you have all workloads installed (including maui-android, etc.)
dotnet build ActualChat.sln
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

If AC_OS environment variable is defined, you're started with Claude Launcher (c.ps1),
so your actual OS is specified in this environment variable and you can use other environment variables
described below to access other projects related to the current one. 
