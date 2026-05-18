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

**Host service connectivity**: The Docker container uses `--network host` mode, so `localhost` inside the container directly refers to the host. This means you can connect to host services (Redis, PostgreSQL, NATS, etc.) using `localhost:port` just like on the host. On macOS, `--network host` requires Docker Desktop 4.34+ (Sept 2024).

**macOS / Apple Silicon**: The Docker image supports both amd64 and arm64 architectures. `c.cmd` is a polyglot script that works on both Windows and macOS/Linux.

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

The user starts Chrome with remote debugging via `c chrome` command (port 9222). On Windows, this also creates a firewall rule to allow connections from WSL/Docker.

**chrome-devtools MCP (preferred over Playwright)**: Up to three `chrome-devtools` MCP servers may be wired up on ports `8765`–`8767`, each bound to its own host Chrome. When they're available (look for `mcp__chrome-devtools-{1,2,3}__*` tools), prefer them over Playwright — and pair them with the `/debug-ui` and `/server-loop` skills if those are available too. Both skills describe the rest.

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

`AC_ProjectRoot` points to the directory that contains all projects. In Docker it is `/proj`, so sibling projects are accessible at `/proj/ActualLab.Fusion`, `/proj/ActualLab.Fusion.Samples`, etc.

| Environment | AC_ProjectRoot | Example sibling project |
|-------------|----------------|-------------------------|
| Docker | `/proj` | `/proj/ActualLab.Fusion` |
| WSL | `/mnt/d/Projects` | `/mnt/d/Projects/ActualLab.Fusion` |
| Windows | `D:\Projects` | `D:\Projects\ActualLab.Fusion` |
| macOS | `~/Projects` | `~/Projects/ActualLab.Fusion` |

## Worktree Support

The launcher supports git worktrees, detected automatically via git.

**Auto-detection**: If you're in a folder like `ActualLab.Fusion-feature1`, the launcher detects it as a worktree of `ActualLab.Fusion` and sets:
- `AC_Worktree` = `feature1`
- `AC_ProjectPath` = path to the worktree folder

**Creating worktrees**: Use the `wt` command to create and switch to a worktree:
```
c wt feature1    # Creates ActualLab.Fusion-feature1 if it doesn't exist and runs there
```

The worktree is created using `git worktree add` from the main project directory.