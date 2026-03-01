#!/usr/bin/env pwsh
# Claude launcher script - runs Claude in Docker, WSL, or native OS

# Auto-detect AC_ProjectRoot from the folder containing this script
# e.g., if c.ps1 is at D:\Projects\ActualChat\c.ps1, AC_ProjectRoot = D:\Projects
if (-not $env:AC_ProjectRoot) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $env:AC_ProjectRoot = Split-Path -Parent $scriptDir
}

# Detect current OS
function Get-CurrentOS {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        return "Windows"
    } elseif ($IsLinux) {
        # Check if running in Docker
        if ((Test-Path "/.dockerenv") -or ((Test-Path "/proc/1/cgroup") -and (Get-Content "/proc/1/cgroup" | Select-String -Pattern "docker|kubepods" -Quiet))) {
            return "Docker"
        }
        # Check if running in WSL
        if (Test-Path "/proc/version") {
            $version = Get-Content "/proc/version"
            if ($version -match "microsoft|WSL") {
                return "WSL"
            }
        }
        return "Linux"
    } elseif ($IsMacOS) {
        return "macOS"
    }
    return "Unknown"
}

# Convert Windows path to WSL path
function ConvertTo-WSLPath {
    param([string]$WindowsPath)
    if ($WindowsPath -match "^([A-Za-z]):(.*)$") {
        $drive = $Matches[1].ToLower()
        $rest = $Matches[2] -replace "\\", "/"
        return "/mnt/$drive$rest"
    }
    return $WindowsPath
}

# Convert Windows path to Docker path (for volume mounts)
function ConvertTo-DockerPath {
    param([string]$WindowsPath)
    # Docker on Windows can use Windows paths directly with forward slashes
    return $WindowsPath -replace "\\", "/"
}

# Check if Windows Terminal is available
function Test-WindowsTerminal {
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        return [bool](Get-Command "wt.exe" -ErrorAction SilentlyContinue)
    }
    return $false
}
$hasWindowsTerminal = Test-WindowsTerminal

# Chrome remote debugging port (standard)
$ChromeDebugPort = 9222

# On Windows, if not already in Windows Terminal, relaunch in wt
# WT_SESSION is set by Windows Terminal when running inside it
# Exception: chrome command runs directly without terminal relaunch
$currentOS = Get-CurrentOS
$hasChrome = $args -contains "chrome"
if ($currentOS -eq "Windows" -and $hasWindowsTerminal -and -not $env:WT_SESSION -and -not $hasChrome) {
    $scriptPath = $MyInvocation.MyCommand.Path
    $workDir = (Get-Location).Path
    # Keep terminal open for build, dry-run, debug, or help (only auto-close when actually running Claude)
    $hasDebug = $args -contains "--debug"
    $hasBuild = $args -contains "build"
    $hasDryRun = $args -contains "--dry-run"
    $hasHelp = $args -contains "help" -or $args -contains "-h" -or $args -contains "--help" -or $args -contains "-?"
    if ($hasDebug -or $hasBuild -or $hasDryRun -or $hasHelp) {
        $wtArgs = @("-d", $workDir, "--", "pwsh", "-NoExit", "-File", $scriptPath) + $args
    } else {
        $wtArgs = @("-d", $workDir, "--", "pwsh", "-File", $scriptPath) + $args
    }
    & wt @wtArgs
    exit 0
}

# Find project root via git. Detects worktrees automatically.
function Find-ProjectRoot {
    param([switch]$Debug)

    $currentPath = (Get-Location).Path
    if ($Debug) { Write-Host "[DEBUG] Starting Find-ProjectRoot, currentPath=$currentPath" }

    # Find git repo root
    $gitRoot = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $gitRoot) {
        if ($Debug) { Write-Host "[DEBUG] Not in a git repository" }
        return $null
    }

    # Normalize: git returns forward slashes; convert to platform separator
    $gitRootNorm = [System.IO.Path]::GetFullPath(($gitRoot -replace "/", [System.IO.Path]::DirectorySeparatorChar))

    if ($Debug) {
        Write-Host "[DEBUG] gitRoot=$gitRootNorm"
    }

    $folderName   = Split-Path -Leaf $gitRootNorm
    $relativePath = ""
    if ($currentPath.Length -gt $gitRootNorm.Length) {
        $relativePath = $currentPath.Substring($gitRootNorm.Length) -replace "\\", "/"
    }

    if ($Debug) { Write-Host "[DEBUG] folderName=$folderName, relativePath=$relativePath" }

    # Detect secondary git worktree: git-common-dir is absolute in worktrees, ".git" in main
    $gitCommonDir = git rev-parse --git-common-dir 2>$null
    $projectName  = $folderName
    $worktree     = ""

    if ($LASTEXITCODE -eq 0 -and $gitCommonDir) {
        $gitCommonDirNorm = $gitCommonDir -replace "/", [System.IO.Path]::DirectorySeparatorChar
        $isAbsolute = [System.IO.Path]::IsPathRooted($gitCommonDirNorm)
        if ($Debug) { Write-Host "[DEBUG] git-common-dir=$gitCommonDir, isAbsolute=$isAbsolute" }

        if ($isAbsolute) {
            # Secondary worktree: common dir is /path/to/main/.git
            $mainProjectPath = Split-Path -Parent ($gitCommonDirNorm.TrimEnd('\', '/'))
            $mainProjectName = Split-Path -Leaf $mainProjectPath
            if ($Debug) { Write-Host "[DEBUG] Worktree detected, mainProject=$mainProjectName" }
            if ($folderName.StartsWith("$mainProjectName-")) {
                $projectName = $mainProjectName
                $worktree    = $folderName.Substring($mainProjectName.Length + 1)
            }
        }
    }

    return @{
        ProjectName   = $projectName   # base project name (without worktree suffix)
        FolderName    = $folderName    # actual folder name on disk
        ProjectRoot   = $gitRootNorm   # full path to project/worktree folder
        RelativePath  = $relativePath  # path from project root to cwd
        Worktree      = $worktree      # worktree suffix, empty if main
    }
}

# Main logic
$currentOS             = Get-CurrentOS
$mode                  = "docker"  # default mode
$fromMode              = $null     # set when self-invoked (e.g., from-docker, from-wsl)
$worktreeSuffix        = $null     # set when wt argument is used
$featureWorktreeSuffix = $null     # set when fwt/bwt argument is used
$wtType                = $null     # worktree type: "feature" or "bugfix"
$newContainer          = $false
$dryRun                = $false
$debugMode             = $false
$claudeArgs            = @()

# Show help
function Show-Help {
    Write-Host "Claude Launcher - Run Claude in Docker, WSL, or native OS" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: c [command] [options] [claude-args]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  (default)    Run Claude in Docker container"
    Write-Host "  os           Run Claude directly on host OS"
    Write-Host "  wsl          Run Claude in WSL (Windows only)"
    Write-Host "  wt <suffix>  Create/use worktree from current branch (e.g., wt experiment)"
    Write-Host "  fwt <suffix> Create/use feature worktree with feat/<suffix> branch (e.g., fwt feature1)"
    Write-Host "  bwt <suffix> Create/use bugfix worktree with bugfix/<suffix> branch (e.g., bwt issue123)"
    Write-Host "  chrome       Start Chrome with remote debugging enabled (for Playwright)"
    Write-Host "  build        Build Docker image for current project"
    Write-Host "  help         Show this help message"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  --new        Force creation of a new Docker container (skip reuse)"
    Write-Host "  --dry-run    Show environment variables and command without executing"
    Write-Host "  --debug      Show debug output for troubleshooting"
    Write-Host ""
    Write-Host "Environment variables (optional):"
    Write-Host "  AC_ProjectRoot    Override auto-detected project root directory"
    Write-Host "  AC_CLAUDE_ISOLATE Set to 'true' or '1' to isolate .claude.json per container instance"
    Write-Host ""
    Write-Host "Environment variables set for Claude:"
    Write-Host "  AC_ProjectRoot    Project root path (/proj in Docker)"
    Write-Host "  AC_ProjectPath    Full path to current project (or worktree)"
    Write-Host "  AC_OS             OS/environment description"
    Write-Host "  AC_Worktree       Worktree suffix (empty if not in a worktree)"
    Write-Host ""
    Write-Host "Docker:"
    Write-Host "  AC_ProjectRoot is mounted as /proj/ — all sibling projects are accessible"
    Write-Host "  Project detection is handled by the project's own CLAUDE.md"
    Write-Host ""
    Write-Host "Worktree support:"
    Write-Host "  Worktrees are auto-detected via git (git rev-parse --git-common-dir)"
    Write-Host "  Use wt  to create a worktree from the current branch"
    Write-Host "  Use fwt to create a feature worktree with a new feat/<suffix> branch"
    Write-Host "  Use bwt to create a bugfix worktree with a new bugfix/<suffix> branch"
    Write-Host "  Base branch for fwt/bwt: 'dev' if it exists on origin, otherwise 'master'"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  c                  Run Claude in Docker"
    Write-Host "  c --dry-run        Show what Docker would run"
    Write-Host "  c os               Run Claude on host OS"
    Write-Host "  c wsl              Run Claude in WSL"
    Write-Host "  c wt experiment    Run in worktree from current branch"
    Write-Host "  c fwt feature1     Run in worktree with feat/feature1 branch"
    Write-Host "  c bwt issue123     Run in worktree with bugfix/issue123 branch"
    Write-Host "  c os fwt feature1  Run on host OS in feature worktree"
    Write-Host "  c os bwt issue1    Run on host OS in bugfix worktree"
    Write-Host "  c chrome           Start Chrome with remote debugging"
    Write-Host "  c build            Build Docker image"
    Write-Host "  c --resume abc     Pass --resume abc to Claude"
    Write-Host ""
}

# Parse arguments
# All c.ps1 commands must come first, then all remaining args go to Claude
$argIndex = 0

# Parse c.ps1 commands (mode, wt, from-*, --dry-run) - must come before Claude args
while ($argIndex -lt $args.Count) {
    $currentArg = $args[$argIndex]

    # Check for mode commands
    if ($currentArg -in "wsl", "os", "build", "chrome" -and $mode -eq "docker") {
        $mode = $currentArg
        $argIndex++
        continue
    }

    # Check for help
    if ($currentArg -in "help", "-h", "--help", "-?") {
        Show-Help
        exit 0
    }

    # Check for from-* argument (indicates self-invocation)
    if ($currentArg -match "^from-(docker|wsl)$") {
        $fromMode = $Matches[1]
        $argIndex++
        continue
    }

    # Check for wt command (regular worktree from current branch)
    if ($currentArg -eq "wt") {
        $argIndex++
        if ($argIndex -lt $args.Count) {
            $worktreeSuffix = $args[$argIndex]
            $argIndex++
        } else {
            Write-Error "The wt command requires a worktree suffix argument"
            exit 1
        }
        continue
    }

    # Check for fwt/bwt command (prefixed branch worktree)
    if ($currentArg -eq "fwt" -or $currentArg -eq "bwt") {
        $wtType = if ($currentArg -eq "fwt") { "feature" } else { "bugfix" }
        $argIndex++
        if ($argIndex -lt $args.Count) {
            $featureWorktreeSuffix = $args[$argIndex]
            # Strip feat/ or bugfix/ prefix if provided (fwt/bwt already adds the prefix)
            $featureWorktreeSuffix = $featureWorktreeSuffix -replace '^(feat|bugfix|hotfix|fix)/', ''
            $argIndex++
        } else {
            Write-Error "The $currentArg command requires a worktree suffix argument"
            exit 1
        }
        continue
    }

    # Check for --dry-run
    if ($currentArg -eq "--dry-run") {
        $dryRun = $true
        $argIndex++
        continue
    }

    # Check for --new (force new Docker container)
    if ($currentArg -eq "--new") {
        $newContainer = $true
        $argIndex++
        continue
    }

    # Check for --debug
    if ($currentArg -eq "--debug") {
        $debugMode = $true
        $argIndex++
        continue
    }

    # Not a c.ps1 command - stop parsing, rest goes to Claude
    break
}

# All remaining args go to Claude
if ($argIndex -lt $args.Count) {
    $claudeArgs = $args[$argIndex..($args.Count - 1)]
}

# Find current project
$projectInfo = Find-ProjectRoot -Debug:$debugMode
if (-not $projectInfo) {
    Write-Error "Could not find project root."
    Write-Error "Make sure you're inside a git repository."
    exit 1
}

$projectName  = $projectInfo.ProjectName
$folderName   = $projectInfo.FolderName
$projectRoot  = $projectInfo.ProjectRoot
$relativePath = $projectInfo.RelativePath -replace "\\", "/"
$worktree     = $projectInfo.Worktree

# Save the original folder name (where c.ps1 was invoked from, before wt/fwt/bwt)
$originalFolderName = $folderName

# Handle wt argument: create regular worktree from current branch and switch to it
if ($worktreeSuffix) {
    # Always use the main project path (not another worktree)
    $mainProjectPath = Join-Path $env:AC_ProjectRoot $projectName
    $worktreePath    = Join-Path $env:AC_ProjectRoot "$projectName-$worktreeSuffix"

    if (-not (Test-Path $worktreePath)) {
        Write-Host "Creating worktree: $projectName-$worktreeSuffix"
        $originalLocation = Get-Location
        Set-Location $mainProjectPath
        try {
            # Get current branch name in the main project
            $currentBranch = git rev-parse --abbrev-ref HEAD
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Failed to get current branch"
                Set-Location $originalLocation
                exit 1
            }

            # Create worktree from current branch
            git worktree add -b $worktreeSuffix $worktreePath $currentBranch
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Failed to create worktree"
                Set-Location $originalLocation
                exit 1
            }

            Write-Host "Created branch '$worktreeSuffix' from '$currentBranch'"
        } finally {
            Set-Location $originalLocation
        }
    }

    # Update project info for the worktree
    $projectRoot  = $worktreePath
    $folderName   = "$projectName-$worktreeSuffix"
    $worktree     = $worktreeSuffix
    $relativePath = ""
    Set-Location $worktreePath
}

# Handle fwt/bwt arguments: create worktree with prefixed branch and switch to it
if ($featureWorktreeSuffix) {
    # Always use the main project path (not another worktree)
    $mainProjectPath = Join-Path $env:AC_ProjectRoot $projectName
    $worktreePath    = Join-Path $env:AC_ProjectRoot "$projectName-$featureWorktreeSuffix"

    if (-not (Test-Path $worktreePath)) {
        Write-Host "Creating $wtType worktree: $projectName-$featureWorktreeSuffix"
        $originalLocation = Get-Location
        Set-Location $mainProjectPath
        try {
            $branchPrefix  = if ($wtType -eq "feature") { "feat" } else { "bugfix" }
            $featureBranch = "$branchPrefix/$featureWorktreeSuffix"

            # Fetch to get up-to-date remote branch info
            git fetch origin 2>$null

            # Auto-detect base branch: prefer dev if it exists on remote, else master
            $null = git rev-parse --verify "refs/remotes/origin/dev" 2>$null
            $baseBranch = if ($LASTEXITCODE -eq 0) { "dev" } else { "master" }

            # Check if the feature branch already exists (locally or remotely)
            $null = git rev-parse --verify "refs/heads/$featureBranch" 2>$null
            $localExists = $LASTEXITCODE -eq 0
            $null = git rev-parse --verify "refs/remotes/origin/$featureBranch" 2>$null
            $remoteExists = $LASTEXITCODE -eq 0

            if (-not $localExists) {
                if ($remoteExists) {
                    # Branch exists on remote but not locally - create local tracking branch
                    Write-Host "Creating local branch '$featureBranch' tracking 'origin/$featureBranch'"
                    git branch $featureBranch "origin/$featureBranch"
                } else {
                    # Branch doesn't exist anywhere - create it from base branch
                    Write-Host "Creating branch '$featureBranch' from 'origin/$baseBranch'"
                    git branch $featureBranch "origin/$baseBranch"
                }
                if ($LASTEXITCODE -ne 0) {
                    Write-Error "Failed to create branch '$featureBranch'"
                    Set-Location $originalLocation
                    exit 1
                }
            } else {
                Write-Host "Using existing branch '$featureBranch'"
            }

            # Create worktree using the existing branch (without -b flag)
            git worktree add $worktreePath $featureBranch
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Failed to create worktree"
                Set-Location $originalLocation
                exit 1
            }
        } finally {
            Set-Location $originalLocation
        }
    }

    # Update project info for the worktree
    $projectRoot  = $worktreePath
    $folderName   = "$projectName-$featureWorktreeSuffix"
    $worktree     = $featureWorktreeSuffix
    $relativePath = ""
    Set-Location $worktreePath
}

# Suppress output when launching docker (inner instance will output)
if ($mode -ne "docker" -or $dryRun) {
    $displayMode = if ($fromMode) { $fromMode } else { $mode }
    Write-Host "Mode: $displayMode"
    if ($dryRun) {
        Write-Host "Dry run: yes"
    }
}

# Helper function: create a volume mount pair (-v host:container[:ro])
function New-VolumeMount {
    param(
        [string]$HostPath,
        [string]$ContainerPath,
        [switch]$ReadOnly,
        [switch]$EnsureExists
    )
    if ($EnsureExists -and -not (Test-Path $HostPath)) {
        New-Item -ItemType Directory -Path $HostPath -Force | Out-Null
    }
    if ($currentOS -eq "Windows") {
        $HostPath = ConvertTo-DockerPath $HostPath
    }
    $mount = "${HostPath}:${ContainerPath}"
    if ($ReadOnly) { $mount += ":ro" }
    return @("-v", $mount)
}

# Helper function: prompt user to select from a list of items
# Returns 0-based index of selected item
function Read-UserSelection {
    param(
        [string]$Title,
        [string[]]$Items,
        [string]$Prompt = "Select"
    )
    Write-Host "${Title}:" -ForegroundColor Cyan
    Write-Host ""
    for ($i = 0; $i -lt $Items.Count; $i++) {
        Write-Host "  [$($i + 1)] $($Items[$i])"
    }
    Write-Host ""
    $choice = Read-Host $Prompt
    if ($choice -match '^\d+$') {
        $idx = [int]$choice - 1
        if ($idx -ge 0 -and $idx -lt $Items.Count) {
            return $idx
        }
    }
    Write-Error "Invalid selection"
    exit 1
}

# Helper function for dry run output
function Show-DryRun {
    param(
        [hashtable]$EnvVars,
        [string]$Command,
        [array]$Arguments,
        [string]$ModeName = "OS"
    )
    Write-Host ""
    Write-Host "=== DRY RUN ($ModeName) ===" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Environment variables for Claude:" -ForegroundColor Cyan
    foreach ($key in $EnvVars.Keys | Sort-Object) {
        Write-Host "  $key=$($EnvVars[$key])"
    }
    Write-Host ""
    Write-Host "Command:" -ForegroundColor Cyan
    Write-Host "  $Command $($Arguments -join ' ')"
    Write-Host ""
}

switch ($mode) {
    "build" {
        # Build Docker image
        $containerName = "claude-$($projectName.ToLower())"
        Write-Host "Building Docker image: $containerName"
        if (-not $dryRun) {
            docker build -t $containerName -f "$projectRoot/claude.Dockerfile" $projectRoot
        } else {
            Write-Host ""
            Write-Host "=== DRY RUN ===" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Command:" -ForegroundColor Cyan
            Write-Host "  docker build -t $containerName -f `"$projectRoot/claude.Dockerfile`" $projectRoot"
            Write-Host ""
        }
    }

    "wsl" {
        if ($currentOS -ne "Windows") {
            Write-Error "WSL mode is only available on Windows"
            exit 1
        }

        # Convert paths for WSL
        $wslProjectRoot = ConvertTo-WSLPath $env:AC_ProjectRoot
        $wslWorkDir = "/mnt/" + ((Get-Location).ToString().Substring(0, 1).ToLower()) + ((Get-Location).ToString().Substring(2) -replace "\\", "/")
        # Use c.ps1 from where it was originally invoked (could be main project or a worktree)
        $wslScriptPath = "$wslProjectRoot/$originalFolderName/c.ps1"

        Write-Host "Working Directory: $wslWorkDir @ $wslProjectRoot"

        # Build args for the script running in WSL
        $wslArgs = @("os", "from-wsl")
        if ($dryRun) { $wslArgs += "--dry-run" }
        if ($debugMode) { $wslArgs += "--debug" }
        $wslArgs += $claudeArgs

        # Build env vars for WSL
        $wslProjectPath = ConvertTo-WSLPath $projectRoot
        $wslEnvString = "AC_ProjectRoot='$wslProjectRoot' DISABLE_AUTOUPDATER=1 AC_ProjectPath='$wslProjectPath' AC_Worktree='$worktree'"

        $wslCommandFull = "cd '$wslWorkDir' && export $wslEnvString && pwsh '$wslScriptPath' $($wslArgs -join ' ')"

        $wslEnvVars = @{
            "AC_ProjectRoot" = $wslProjectRoot
            "AC_ProjectPath" = $wslProjectPath
            "AC_OS"          = "Linux on WSL"
            "AC_Worktree"    = $worktree
        }

        if ($dryRun) {
            Write-Host ""
            Write-Host "=== DRY RUN (WSL) ===" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Environment variables for Claude:" -ForegroundColor Cyan
            foreach ($key in $wslEnvVars.Keys | Sort-Object) {
                Write-Host "  $key=$($wslEnvVars[$key])"
            }
            Write-Host ""
            Write-Host "Command:" -ForegroundColor Cyan
            $cmdLine = ("claude " + ($claudeArgs -join ' ')).Trim()
            Write-Host "  $cmdLine"
            Write-Host ""
            Write-Host "WSL launch command:" -ForegroundColor Cyan
            Write-Host "  wsl bash -c `"$wslCommandFull`""
            Write-Host ""
        } else {
            wsl bash -c $wslCommandFull
        }
    }

    "os" {
        # Run Claude directly on the host OS
        $env:AC_ProjectPath = $projectRoot
        $env:AC_Worktree    = $worktree
        $env:DISABLE_AUTOUPDATER = "1"

        # Set AC_OS based on detected environment
        $env:AC_OS = switch ($currentOS) {
            "Docker" { "Linux in Docker" }
            "WSL"    { "Linux on WSL" }
            default  { $currentOS }
        }

        Write-Host "Running Claude on: $($env:AC_OS)"
        Write-Host "Working Directory: $(Get-Location) @ $env:AC_ProjectRoot"
        if ($worktree) {
            Write-Host "Worktree: $worktree"
        }

        $envVars = @{
            "AC_ProjectRoot" = $env:AC_ProjectRoot
            "AC_ProjectPath" = $env:AC_ProjectPath
            "AC_OS"          = $env:AC_OS
            "AC_Worktree"    = $env:AC_Worktree
        }

        if ($dryRun) {
            $allArgs = if ($currentOS -eq "Docker") {
                @("--dangerously-skip-permissions") + $claudeArgs
            } else {
                $claudeArgs
            }
            Show-DryRun -EnvVars $envVars -Command "claude" -Arguments $allArgs -ModeName $env:AC_OS
        } else {
            # Only skip permissions in Docker (sandboxed environment)
            if ($currentOS -eq "Docker") {
                & claude --dangerously-skip-permissions @claudeArgs
                if ($debugMode) {
                    Write-Host ""
                    Read-Host "Press Enter to close..."
                }
            } elseif ($currentOS -eq "WSL") {
                # In WSL, use bash -i to run claude (interactive shell sources .bashrc with npm PATH)
                $claudeCmd = ("claude " + ($claudeArgs -join ' ')).Trim()
                & bash -i -c $claudeCmd
            } else {
                # Windows/Linux/macOS - already in wt on Windows (handled at script start)
                & claude @claudeArgs
            }
        }
    }

    "docker" {
        $homeDir = if ($currentOS -eq "Windows") { $env:USERPROFILE } else { $env:HOME }
        $volumeMounts = @()

        # Mount entire AC_ProjectRoot as /proj/ — all sibling projects are visible
        $volumeMounts += New-VolumeMount $env:AC_ProjectRoot "/proj"

        # Artifact/node_modules overrides for current project only (avoids permission conflicts with host)
        $currentFolderName  = if ($worktree) { "$projectName-$worktree" } else { $projectName }
        $currentHostPath    = Join-Path $env:AC_ProjectRoot $currentFolderName
        $artifactsHostPath  = Join-Path $currentHostPath "artifacts" "claude-docker"
        $volumeMounts += New-VolumeMount $artifactsHostPath "/proj/$currentFolderName/artifacts" -EnsureExists

        # node_modules from artifacts/claude-docker for persistence across container restarts
        $nodeModulesHostPath  = Join-Path $artifactsHostPath "node_modules"
        $nodeModulesMountPoint = Join-Path $currentHostPath "node_modules"
        if (-not (Test-Path $nodeModulesMountPoint)) {
            New-Item -ItemType Directory -Path $nodeModulesMountPoint -Force | Out-Null
        }
        $volumeMounts += New-VolumeMount $nodeModulesHostPath "/proj/$currentFolderName/node_modules" -EnsureExists

        # Claude config mounts
        $volumeMounts += New-VolumeMount "$homeDir/.claude" "/home/claude/.claude"

        # Handle .claude.json mounting
        $claudeJsonPath = "$homeDir/.claude.json"
        if ($env:AC_CLAUDE_ISOLATE -iin "true", "1") {
            # Isolated mode: copy .claude.json to a unique file per instance
            $instanceId = Get-Date -Format "yyyyMMdd-HHmmss-fff"
            $isolateDir = Join-Path $projectRoot "artifacts" "claude-docker"
            if (-not (Test-Path $isolateDir)) {
                New-Item -ItemType Directory -Path $isolateDir -Force | Out-Null
            }
            if (Test-Path $claudeJsonPath) {
                $isolatedClaudeJson = Join-Path $isolateDir ".claude-$instanceId.json"
                Copy-Item $claudeJsonPath $isolatedClaudeJson
                $volumeMounts += New-VolumeMount $isolatedClaudeJson "/home/claude/.claude.json"
            }
            Write-Host "Claude isolation: enabled (instance: $instanceId)" -ForegroundColor Cyan
        } else {
            # Normal mode: mount .claude.json directly from host
            if (Test-Path $claudeJsonPath) {
                $volumeMounts += New-VolumeMount $claudeJsonPath "/home/claude/.claude.json"
            }
        }

        # Git config mount
        $gitConfigPath = "$homeDir/.gitconfig"
        if (Test-Path $gitConfigPath) {
            $volumeMounts += New-VolumeMount $gitConfigPath "/home/claude/.gitconfig" -ReadOnly
        }

        # Gcloud config mount
        $gcloudConfigPath = if ($currentOS -eq "Windows") { "$env:APPDATA/gcloud" } else { "$homeDir/.config/gcloud" }
        if (Test-Path $gcloudConfigPath) {
            $volumeMounts += New-VolumeMount $gcloudConfigPath "/home/claude/.config/gcloud" -ReadOnly
        }

        # GCP key folder mount (for GOOGLE_APPLICATION_CREDENTIALS)
        $gcpKeyPath = "$homeDir/.gcp"
        if (Test-Path $gcpKeyPath) {
            $volumeMounts += New-VolumeMount $gcpKeyPath "/home/claude/.gcp" -ReadOnly
        }

        # .actual folder mount (project-agnostic; contains prompts and other config)
        $actualPath = "$homeDir/.actual"
        if (Test-Path $actualPath) {
            $volumeMounts += New-VolumeMount $actualPath "/home/claude/.actual" -ReadOnly
        }

        # Calculate Docker working directory
        $dockerWorkDir     = "/proj/$currentFolderName$relativePath"
        $imageName         = "claude-$($projectName.ToLower())"
        $containerBaseName = if ($worktree) { "$($projectName.ToLower())-$($worktree.ToLower())" } else { $projectName.ToLower() }
        $containerName     = "$containerBaseName-$(Get-Date -Format 'MMdd-HHmmss')"
        # Use c.ps1 from where it was originally invoked (could be main project or a worktree)
        $dockerScriptPath = "/proj/$originalFolderName/c.ps1"

        if ($dryRun) {
            Write-Host "Container: $containerName"
            Write-Host "Working Directory: $dockerWorkDir @ /proj"
        }

        # Build args for the script running in Docker
        $dockerScriptArgs = @("os", "from-docker")
        if ($dryRun) { $dockerScriptArgs += "--dry-run" }
        if ($debugMode) { $dockerScriptArgs += "--debug" }
        $dockerScriptArgs += $claudeArgs

        # Container reuse logic (default unless --new is specified)
        if (-not $newContainer) {
            $existingContainers = @(docker ps --filter "label=worktree=$containerBaseName" --format "{{.ID}}`t{{.Names}}`t{{.Status}}" 2>$null | Where-Object { $_ })
            $selectedContainer = $null
            if ($existingContainers.Count -eq 1) {
                $selectedContainer = $existingContainers[0]
            } elseif ($existingContainers.Count -gt 1) {
                $displayItems = $existingContainers | ForEach-Object { $p = $_ -split "`t"; "$($p[1]) ($($p[2]))" }
                $idx = Read-UserSelection `
                    -Title "Multiple containers found for '$containerBaseName'" `
                    -Items $displayItems `
                    -Prompt "Select container"
                $selectedContainer = $existingContainers[$idx]
            }
            if ($selectedContainer) {
                $parts = $selectedContainer -split "`t"
                $containerId          = $parts[0]
                $containerDisplayName = $parts[1]
                if ($dryRun) {
                    Write-Host ""
                    Write-Host "=== DRY RUN (Docker - reuse) ===" -ForegroundColor Yellow
                    Write-Host ""
                    Write-Host "Would reuse container: $containerDisplayName" -ForegroundColor Cyan
                    Write-Host "Command:" -ForegroundColor Cyan
                    Write-Host "  docker exec -it -w $dockerWorkDir $containerId pwsh $dockerScriptPath $($dockerScriptArgs -join ' ')"
                    Write-Host ""
                } else {
                    Write-Host "Reusing container: $containerDisplayName" -ForegroundColor Cyan
                    $execArgs = @("exec", "-it", "-w", $dockerWorkDir, $containerId, "pwsh", $dockerScriptPath) + $dockerScriptArgs
                    & docker @execArgs
                }
                exit $LASTEXITCODE
            }
            # No container selected - fall through to create new
        }

        # Build project path env vars for Docker
        $dockerProjectPath = "/proj/$currentFolderName"
        $projectEnvVars = @("-e", "AC_ProjectPath=$dockerProjectPath", "-e", "AC_Worktree=$worktree")

        # Collect environment variables to propagate:
        # - Variables with __ in their names (e.g., ChatSettings__OpenAIApiKey)
        # - GITHUB_TOKEN, NPM_READ_TOKEN, GOOGLE_CLOUD_PROJECT
        # - ActualChat_* variables
        $propagatedEnvVars = @()
        Get-ChildItem env: | ForEach-Object {
            $name  = $_.Name
            $value = $_.Value
            if ($name -match '__' -or
                $name -eq 'GITHUB_TOKEN' -or
                $name -eq 'NPM_READ_TOKEN' -or
                $name -eq 'GOOGLE_CLOUD_PROJECT' -or
                $name -like 'ActualChat_*') {
                $propagatedEnvVars += "-e"
                $propagatedEnvVars += "$name=$value"
            }
        }

        # Set GOOGLE_APPLICATION_CREDENTIALS to container path (host path won't work)
        if ($env:GOOGLE_APPLICATION_CREDENTIALS) {
            $propagatedEnvVars += "-e"
            $propagatedEnvVars += "GOOGLE_APPLICATION_CREDENTIALS=/home/claude/.gcp/key.json"
        }

        # Build docker run command - run this script with "os" argument inside container
        # Uses --network host so localhost inside container = host's localhost
        $dockerArgs = @(
            "run", "-it", "--rm"
            "--network", "host"
            "--name", $containerName
            "--label", "worktree=$containerBaseName"
        )

        $dockerArgs += $volumeMounts + @(
            "-e", "ANTHROPIC_API_KEY=$env:ANTHROPIC_API_KEY"
            "-e", "Claude_GeminiAPIKey=$env:Claude_GeminiAPIKey"
            "-e", "DISABLE_AUTOUPDATER=1"
            "-e", "DOTNET_SYSTEM_NET_DISABLEIPV6=1"
            "-e", "AC_ProjectRoot=/proj"
        ) + $projectEnvVars + $propagatedEnvVars

        $dockerArgs += @(
            "-w", $dockerWorkDir
            $imageName
            "pwsh", $dockerScriptPath
        ) + $dockerScriptArgs

        if ($dryRun) {
            # Build env vars hashtable for display
            $dockerEnvVars = @{
                "AC_ProjectRoot" = "/proj"
                "AC_ProjectPath" = $dockerProjectPath
                "AC_OS"          = "Linux in Docker"
                "AC_Worktree"    = $worktree
            }

            Write-Host ""
            Write-Host "=== DRY RUN (Docker) ===" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Environment variables for Claude:" -ForegroundColor Cyan
            foreach ($key in $dockerEnvVars.Keys | Sort-Object) {
                Write-Host "  $key=$($dockerEnvVars[$key])"
            }
            Write-Host ""
            Write-Host "Command:" -ForegroundColor Cyan
            Write-Host "  claude --dangerously-skip-permissions $($claudeArgs -join ' ')"
            Write-Host ""
            Write-Host "Docker launch command:" -ForegroundColor Cyan
            Write-Host "  docker $($dockerArgs -join ' ')"
            Write-Host ""
        } else {
            # On Windows, we're already in wt (handled at script start)
            & docker @dockerArgs
        }
    }

    "chrome" {
        # Start Chrome with remote debugging enabled

        # On Windows, ensure firewall rule exists for remote debugging port
        if ($currentOS -eq "Windows") {
            $firewallRuleName = "Chrome Remote Debugging (Claude)"

            # Check if firewall rule exists
            $ruleExists = $false
            try {
                $existingRule = netsh advfirewall firewall show rule name="$firewallRuleName" 2>$null
                $ruleExists = $LASTEXITCODE -eq 0 -and $existingRule
            } catch {
                $ruleExists = $false
            }

            if (-not $ruleExists) {
                Write-Host "Creating firewall rule for Chrome remote debugging (port $ChromeDebugPort)..." -ForegroundColor Cyan

                # Try to add the firewall rule
                $result = netsh advfirewall firewall add rule `
                    name="$firewallRuleName" `
                    dir=in `
                    action=allow `
                    protocol=tcp `
                    localport=$ChromeDebugPort `
                    profile=private `
                    description="Allow Chrome remote debugging connections from WSL/Docker" 2>&1

                if ($LASTEXITCODE -ne 0) {
                    # Check if it's a permission error
                    if ($result -match "requires elevation|Access is denied|administrator") {
                        Write-Host ""
                        Write-Host "Failed to create firewall rule - administrator privileges required." -ForegroundColor Yellow
                        Write-Host "Please run this command in an elevated PowerShell:" -ForegroundColor Yellow
                        Write-Host ""
                        Write-Host "  netsh advfirewall firewall add rule name=`"$firewallRuleName`" dir=in action=allow protocol=tcp localport=$ChromeDebugPort profile=private" -ForegroundColor White
                        Write-Host ""
                        Write-Host "Or re-run 'c chrome' as Administrator." -ForegroundColor Yellow
                        exit 1
                    } else {
                        Write-Host "Warning: Failed to create firewall rule: $result" -ForegroundColor Yellow
                        Write-Host "Connections from WSL/Docker may not work." -ForegroundColor Yellow
                    }
                } else {
                    Write-Host "Firewall rule created successfully." -ForegroundColor Green
                }
            }
        }

        # Helper to check if debug port is open
        function Test-DebugPort {
            if ($currentOS -eq "Windows") {
                $listening = netstat -an | Select-String ":$ChromeDebugPort\s+.*LISTENING"
                return $null -ne $listening
            } else {
                # Unix/macOS: use lsof or nc
                $result = bash -c "lsof -i :$ChromeDebugPort -sTCP:LISTEN 2>/dev/null || nc -z localhost $ChromeDebugPort 2>/dev/null"
                return $LASTEXITCODE -eq 0
            }
        }

        # Check if Chrome is already running with debug port
        if (Test-DebugPort) {
            Write-Host "Chrome is already running with remote debugging on port $ChromeDebugPort" -ForegroundColor Yellow
            exit 0
        }

        # Find Chrome executable and user data dir based on OS
        if ($currentOS -eq "Windows") {
            $chromePaths = @(
                "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
                "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
                "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
            )
            $debugUserDataDir = "$env:LOCALAPPDATA\Google\Chrome\Playwright"
        } elseif ($currentOS -eq "macOS") {
            $chromePaths = @(
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
            )
            $debugUserDataDir = "$env:HOME/Library/Application Support/Google/Chrome Playwright"
        } else {
            # Linux
            $chromePaths = @(
                "/usr/bin/google-chrome",
                "/usr/bin/google-chrome-stable",
                "/usr/bin/chromium-browser",
                "/usr/bin/chromium"
            )
            $debugUserDataDir = "$env:HOME/.config/google-chrome-playwright"
        }

        $chromePath = $null
        foreach ($path in $chromePaths) {
            if (Test-Path $path) {
                $chromePath = $path
                break
            }
        }

        if (-not $chromePath) {
            Write-Error "Chrome not found. Please install Google Chrome."
            exit 1
        }

        Write-Host "Starting Chrome with remote debugging on port $ChromeDebugPort..." -ForegroundColor Cyan
        Write-Host "Chrome path: $chromePath"

        # Use a separate user data dir to force a new Chrome instance (otherwise Chrome reuses existing process and ignores debug flag)
        # Start Chrome with remote debugging in a new process
        # --remote-debugging-address=0.0.0.0 allows connections from WSL/Docker (not just localhost)
        Start-Process -FilePath $chromePath -ArgumentList "--remote-debugging-port=$ChromeDebugPort", "--remote-debugging-address=0.0.0.0", "--user-data-dir=`"$debugUserDataDir`"", "--remote-allow-origins=*"

        # Wait for debug port to open (check once per second, max 30 seconds)
        # First 2 checks are silent, then show waiting message
        $maxWait = 30
        $waited = 0
        $printedWaiting = $false
        while (-not (Test-DebugPort) -and $waited -lt $maxWait) {
            Start-Sleep -Seconds 1
            $waited++
            if ($waited -gt 2 -and -not $printedWaiting) {
                Write-Host "Waiting for the debug port to open: " -NoNewline
                $printedWaiting = $true
            }
            if ($printedWaiting) {
                Write-Host "." -NoNewline
            }
        }
        if ($printedWaiting) {
            Write-Host ""
        }

        if (Test-DebugPort) {
            Write-Host "Chrome started with remote debugging on port $ChromeDebugPort" -ForegroundColor Green
            Write-Host "Note: This uses a separate profile (Playwright)" -ForegroundColor DarkGray
        } else {
            Write-Host "Timed out waiting for Chrome debug port." -ForegroundColor Yellow
        }
    }
}
