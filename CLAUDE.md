Before starting any task, read AGENTS.md files in every directory starting from the current one and above, up to the root one (project directory).

`pwsh` (cross-platform PowerShell) command is available on any OS you run, so use it.

If AC_OS environment variable is defined, you're started with Claude Launcher (c.ps1), 
so your actual OS is specified in this environment variable and you can use other environment variables 
described below to access other projects related to the current one. 

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
