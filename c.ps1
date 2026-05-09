#!/usr/bin/env pwsh
# Claude launcher script - runs Claude in Docker, WSL, or native OS

# Auto-detect AC_ProjectRoot from the folder containing this script
# e.g., if c.ps1 is at D:\Projects\ActualChat\c.ps1, AC_ProjectRoot = D:\Projects
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $env:AC_ProjectRoot) {
    $env:AC_ProjectRoot = Split-Path -Parent $scriptDir
}

# Load common utilities
. (Join-Path $scriptDir "scripts/Common.ps1")

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

# Chrome remote debugging port (standard) and multi-instance defaults.
# The `chrome` command supports `chrome[:PORT][*N]` (N=1..9). When `*N` is
# given, each instance gets its own anonymous profile so cookies don't bleed
# across them — useful for testing multi-user flows.
$ChromeDebugPort           = 9222     # legacy single-port default (also exported to Docker for the chrome-devtools MCP)
$ChromeDebugStartPort      = $ChromeDebugPort
$ChromeInstanceCount       = 1
$ChromeUseAnonymousProfile = $false
$ChromeArgPattern          = '^chrome(?:[:*]\d+){0,2}$'
$ChromeExtraArgs           = @()

# Edge mirrors the Chrome shape but defaults to a different start port so the
# two can run side by side without the firewall/port-collision dance.
$EdgeDebugPort             = 9322
$EdgeDebugStartPort        = $EdgeDebugPort
$EdgeInstanceCount         = 1
$EdgeUseAnonymousProfile   = $false
$EdgeArgPattern            = '^edge(?:[:*]\d+){0,2}$'
$EdgeExtraArgs             = @()

# On Windows, if not already in Windows Terminal, relaunch in wt
# WT_SESSION is set by Windows Terminal when running inside it
# Exception: chrome command runs directly without terminal relaunch
$currentOS = Get-CurrentOS
$hasChrome = ($args | Where-Object { $_ -match $ChromeArgPattern }).Count -gt 0
$hasEdge   = ($args | Where-Object { $_ -match $EdgeArgPattern   }).Count -gt 0
if ($currentOS -eq "Windows" -and $hasWindowsTerminal -and -not $env:WT_SESSION -and -not $hasChrome -and -not $hasEdge) {
    $scriptPath = $MyInvocation.MyCommand.Path
    $workDir = (Get-Location).Path
    # Keep terminal open for build, dry-run, debug, or help (only auto-close when actually running Claude)
    $hasDebug = $args -contains "--debug"
    $hasBuild = $args -contains "build"
    $hasDryRun = $args -contains "--dry-run"
    $hasHelp = $args -contains "help" -or $args -contains "-h" -or $args -contains "--help" -or $args -contains "-?"
    if ($hasDebug -or $hasBuild -or $hasDryRun -or $hasHelp) {
        $wtArgs = @("-d", $workDir, "--", "pwsh", "-NoProfile", "-NoExit", "-File", $scriptPath) + $args
    } else {
        $wtArgs = @("-d", $workDir, "--", "pwsh", "-NoProfile", "-File", $scriptPath) + $args
    }
    & wt @wtArgs
    exit 0
}

# Convert worktree git paths from absolute to relative so they work across
# Windows and Docker/Linux (where mount points differ).
function Convert-WorktreeToRelativePaths {
    param(
        [string]$WorktreePath,
        [string]$MainProjectPath,
        [switch]$Debug
    )

    $worktreeName = Split-Path -Leaf $WorktreePath

    # Fix <worktree>/.git file: convert absolute gitdir to relative
    $dotGitFile = Join-Path $WorktreePath ".git"
    if (Test-Path $dotGitFile) {
        $content = Get-Content $dotGitFile -Raw
        if ($content -match '^gitdir:\s*(.+)$') {
            $currentGitDir = $Matches[1].Trim()
            # Only fix if the path is absolute (not already relative)
            if ([System.IO.Path]::IsPathRooted(($currentGitDir -replace "/", [System.IO.Path]::DirectorySeparatorChar))) {
                $relPath = [System.IO.Path]::GetRelativePath($WorktreePath, ($currentGitDir -replace "/", [System.IO.Path]::DirectorySeparatorChar))
                $relPath = $relPath -replace "\\", "/"
                $newContent = "gitdir: $relPath`n"
                Set-Content -Path $dotGitFile -Value $newContent -NoNewline
                if ($Debug) { Write-Host "[DEBUG] Fixed $dotGitFile`: gitdir: $relPath" }
            } elseif ($Debug) {
                Write-Host "[DEBUG] $dotGitFile already has relative path: $currentGitDir"
            }
        }
    }

    # Fix <main>/.git/worktrees/<name>/gitdir: convert absolute path to relative
    $mainGitDir = Join-Path $MainProjectPath ".git"
    $worktreeGitDir = Join-Path $mainGitDir "worktrees" $worktreeName "gitdir"
    if (Test-Path $worktreeGitDir) {
        $content = (Get-Content $worktreeGitDir -Raw).Trim()
        if ([System.IO.Path]::IsPathRooted(($content -replace "/", [System.IO.Path]::DirectorySeparatorChar))) {
            $worktreeGitDirParent = Split-Path -Parent $worktreeGitDir
            $relPath = [System.IO.Path]::GetRelativePath($worktreeGitDirParent, ($content -replace "/", [System.IO.Path]::DirectorySeparatorChar))
            $relPath = $relPath -replace "\\", "/"
            Set-Content -Path $worktreeGitDir -Value "$relPath`n" -NoNewline
            if ($Debug) { Write-Host "[DEBUG] Fixed $worktreeGitDir`: $relPath" }
        } elseif ($Debug) {
            Write-Host "[DEBUG] $worktreeGitDir already has relative path: $content"
        }
    }
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
$removeWorktreeSuffix  = $null     # set when rwt argument is used
$wtType                = $null     # worktree type: "feature" or "bugfix"
$newContainer          = $false
$renewContainer        = $false
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
    Write-Host "  rwt <suffix> Remove worktree and clean up (ports, hosts, nginx config)"
    Write-Host "  chrome       Start Chrome with remote debugging enabled (for Playwright)"
    Write-Host "  build        Build Docker image for current project"
    Write-Host "  help         Show this help message"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  --new        Force creation of a new Docker container (skip reuse)"
    Write-Host "  --renew      Remove existing containers and start a new one (use after image rebuild)"
    Write-Host "  --dry-run    Show environment variables and command without executing"
    Write-Host "  --debug      Show debug output for troubleshooting"
    Write-Host ""
    Write-Host "Environment variables (optional):"
    Write-Host "  AC_ProjectRoot    Override auto-detected project root directory"
    Write-Host "  AC_CLAUDE_ISOLATE Set to 'true' or '1' to isolate .claude.json per container instance"
    Write-Host ""
    Write-Host "Environment variables set for Claude:"
    Write-Host "  AC_ProjectRoot      Project root path (/proj in Docker)"
    Write-Host "  AC_ProjectPath      Full path to current project (or worktree)"
    Write-Host "  AC_OS               OS/environment description"
    Write-Host "  AC_Worktree         Worktree suffix (empty if not in a worktree)"
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
    Write-Host "  c rwt feature1     Remove feature1 worktree and clean up"
    Write-Host "  c os fwt feature1  Run on host OS in feature worktree"
    Write-Host "  c os bwt issue1    Run on host OS in bugfix worktree"
    Write-Host "  c chrome           Start Chrome with remote debugging (default profile, port 9222)"
    Write-Host "  c chrome:50000     Start Chrome on port 50000 (default profile)"
    Write-Host "  c chrome*3         Start 3 Chrome instances on 9222..9224 (anonymous profiles)"
    Write-Host "  c chrome*3:50000   Start 3 Chrome instances on 50000..50002 (anonymous profiles)"
    Write-Host "  c chrome --mute-audio --window-size=1280,720"
    Write-Host "                     Any args after chrome[*N][:PORT] are forwarded to the browser"
    Write-Host "  c chrome --fake-media"
    Write-Host "                     Use synthetic camera/mic streams (default is real devices)"
    Write-Host "  c edge[:PORT][*N]  Same as chrome, for Microsoft Edge (default port 9322)"
    Write-Host "  c audio            Setup/start PulseAudio for voice mode (macOS only)"
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
    if ($currentArg -in "wsl", "os", "build", "audio" -and $mode -eq "docker") {
        $mode = $currentArg
        $argIndex++
        continue
    }

    # Chrome command: `chrome`, `chrome:PORT`, `chrome*N`, `chrome:PORT*N`, `chrome*N:PORT`
    # Any further args (e.g. `--mute-audio`, `--window-size=...`) are forwarded
    # verbatim to the launched browser process.
    if ($currentArg -match $ChromeArgPattern -and $mode -eq "docker") {
        $mode = "chrome"
        if ([regex]::Match($currentArg, ':(\d+)').Success) {
            $ChromeDebugStartPort = [int][regex]::Match($currentArg, ':(\d+)').Groups[1].Value
        }
        if ([regex]::Match($currentArg, '\*(\d+)').Success) {
            $n = [int][regex]::Match($currentArg, '\*(\d+)').Groups[1].Value
            if ($n -lt 1 -or $n -gt 9) {
                Write-Error "chrome: instance count must be between 1 and 9 (got $n)"
                exit 1
            }
            $ChromeInstanceCount = $n
            $ChromeUseAnonymousProfile = $true
        }
        $argIndex++
        if ($argIndex -lt $args.Count) {
            $ChromeExtraArgs = $args[$argIndex..($args.Count - 1)]
            $argIndex = $args.Count
        }
        continue
    }

    # Edge command: same shape as chrome (`edge`, `edge:PORT`, `edge*N`, `edge:PORT*N`, `edge*N:PORT`).
    # Any further args are forwarded to the browser process, same as chrome.
    if ($currentArg -match $EdgeArgPattern -and $mode -eq "docker") {
        $mode = "edge"
        if ([regex]::Match($currentArg, ':(\d+)').Success) {
            $EdgeDebugStartPort = [int][regex]::Match($currentArg, ':(\d+)').Groups[1].Value
        }
        if ([regex]::Match($currentArg, '\*(\d+)').Success) {
            $n = [int][regex]::Match($currentArg, '\*(\d+)').Groups[1].Value
            if ($n -lt 1 -or $n -gt 9) {
                Write-Error "edge: instance count must be between 1 and 9 (got $n)"
                exit 1
            }
            $EdgeInstanceCount = $n
            $EdgeUseAnonymousProfile = $true
        }
        $argIndex++
        if ($argIndex -lt $args.Count) {
            $EdgeExtraArgs = $args[$argIndex..($args.Count - 1)]
            $argIndex = $args.Count
        }
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
            # Strip feat/ or bugfix/ prefix if provided (fwt/bwt already adds the prefix)
            $featureWorktreeSuffix = $args[$argIndex] -replace '^(feat|bugfix|hotfix|fix)/', ''
            $argIndex++
        } else {
            Write-Error "The $currentArg command requires a worktree suffix argument"
            exit 1
        }
        continue
    }

    # Check for rwt command (remove worktree)
    if ($currentArg -eq "rwt") {
        $argIndex++
        if ($argIndex -lt $args.Count) {
            # Strip feat/ or bugfix/ prefix if provided (to match how fwt/bwt creates folders)
            $removeWorktreeSuffix = $args[$argIndex] -replace '^(feat|bugfix|hotfix|fix)/', ''
            $argIndex++
        } else {
            Write-Error "The rwt command requires a worktree suffix argument"
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

    # Check for --renew (remove existing containers and start fresh)
    if ($currentArg -eq "--renew") {
        $renewContainer = $true
        $newContainer = $true  # --renew implies --new
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
if ($fromMode -and $env:AC_ProjectPath) {
    # Re-invoked inside Docker/WSL: derive project info from env vars set by outer invocation.
    # This avoids calling git rev-parse, which fails in worktrees with absolute Windows paths.
    $projectRoot  = $env:AC_ProjectPath
    $folderName   = Split-Path -Leaf $env:AC_ProjectPath
    $worktree     = if ($env:AC_Worktree) { $env:AC_Worktree } else { "" }
    $projectName  = if ($worktree -and $folderName.EndsWith("-$worktree")) {
        $folderName.Substring(0, $folderName.Length - $worktree.Length - 1)
    } else { $folderName }
    $relativePath = ""
    if ($debugMode) {
        Write-Host "[DEBUG] Using env vars: projectRoot=$projectRoot, folderName=$folderName, worktree=$worktree, projectName=$projectName"
    }

    # Fix worktree git paths if they still have absolute Windows paths (from host)
    if ($worktree) {
        $mainProjectPath = Join-Path $env:AC_ProjectRoot $projectName
        Convert-WorktreeToRelativePaths -WorktreePath $projectRoot -MainProjectPath $mainProjectPath -Debug:$debugMode
    }
} else {
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
}

# Save the original folder name (where c.ps1 was invoked from, before wt/fwt/bwt)
$originalFolderName = $folderName

# Port registry for worktree server ports
class PortRegistry {
    [string]$ProjectPath
    [string]$RegistryPath
    [int]$BasePort = 7080
    [int]$PortIncrement = 10
    [int]$MaxPort = 7370

    PortRegistry([string]$projectPath) {
        $this.ProjectPath = $projectPath
        $this.RegistryPath = Join-Path $projectPath "artifacts" "server-ports.json"
    }

    hidden [hashtable] Load() {
        if (Test-Path $this.RegistryPath) {
            return Get-Content $this.RegistryPath -Raw | ConvertFrom-Json -AsHashtable
        }
        return @{ "dev" = $this.BasePort }
    }

    hidden [void] Save([hashtable]$registry) {
        $registry | ConvertTo-Json -Depth 10 | Set-Content $this.RegistryPath
    }

    [object] Get([string]$instanceName) {
        $registry = $this.Load()
        if ($registry.ContainsKey($instanceName)) {
            return $registry[$instanceName]
        }
        return $null
    }

    [int] Allocate([string]$instanceName) {
        $registry = $this.Load()

        # Return existing port if already allocated
        if ($registry.ContainsKey($instanceName)) {
            return $registry[$instanceName]
        }

        # Allocate new port
        $usedPorts = [System.Collections.Generic.HashSet[int]]::new([int[]]@($registry.Values))
        $port = $this.BasePort
        while ($usedPorts.Contains($port) -and $port -le $this.MaxPort) {
            $port += $this.PortIncrement
        }

        if ($port -gt $this.MaxPort) {
            throw "No more port blocks available. Maximum port $($this.MaxPort) exceeded."
        }

        $registry[$instanceName] = $port
        $this.Save($registry)

        return $port
    }

    [bool] Deallocate([string]$instanceName) {
        if (-not (Test-Path $this.RegistryPath)) {
            return $false
        }

        $registry = $this.Load()
        if (-not $registry.ContainsKey($instanceName)) {
            return $false
        }

        $registry.Remove($instanceName)
        $this.Save($registry)

        return $true
    }
}

# Worktree server configuration and registration
class WorktreeServer {
    [string]$ProjectPath
    [string]$WorktreeSuffix
    [string]$InstanceName
    [int]$Port
    [string[]]$Hostnames
    [PortRegistry]$PortRegistry
    [bool]$IsMainProject

    WorktreeServer([string]$projectPath, [string]$worktreeSuffix) {
        $this.ProjectPath = $projectPath
        $this.WorktreeSuffix = $worktreeSuffix
        $this.IsMainProject = -not $worktreeSuffix

        # Truncate suffix for domain/instance names (max 20 chars), default to "dev" for main project
        $this.InstanceName = if ($worktreeSuffix) {
            -join $worktreeSuffix[0..([Math]::Min(19, $worktreeSuffix.Length - 1))]
        } else { "dev" }

        # Build hostnames for this worktree (main project doesn't need custom hostnames).
        $this.Hostnames = if (-not $this.IsMainProject) {
            @(
                "$($this.InstanceName).local.voxt.ai",
                "cdn-$($this.InstanceName).local.voxt.ai",
                "media-$($this.InstanceName).local.voxt.ai"
            )
        } else { @() }

        $this.PortRegistry = [PortRegistry]::new($projectPath)
        $this.Port = $this.PortRegistry.Get($this.InstanceName)
    }

    [hashtable] GetConfig() {
        return @{
            InstanceName = $this.InstanceName
            Port         = $this.Port
        }
    }

    [hashtable] Register([bool]$debug) {
        # Return existing config if already registered
        if ($this.Port) {
            return $this.GetConfig()
        }

        # Allocate new port
        $this.Port = $this.PortRegistry.Allocate($this.InstanceName)
        if ($debug) { Write-Host "[DEBUG] Allocated port $($this.Port) for instance '$($this.InstanceName)'" }

        # Write nginx config and update hosts (only for worktrees, not main project)
        if (-not $this.IsMainProject) {
            $this.WriteNginxConfig($debug)
            $this.ReloadNginx($debug)
            $this.AddHostsEntries($debug)
        }

        return $this.GetConfig()
    }

    [void] Unregister([bool]$debug) {
        if ($this.IsMainProject) { return }

        # Remove from port registry
        $removed = $this.PortRegistry.Deallocate($this.InstanceName)
        if (-not $removed) {
            if ($debug) { Write-Host "[DEBUG] Instance '$($this.InstanceName)' not found in registry" }
            return
        }
        if ($debug) { Write-Host "[DEBUG] Removed instance '$($this.InstanceName)' from server registry" }

        $this.RemoveNginxConfig($debug)
        $this.ReloadNginx($debug)
        $this.RemoveHostsEntries($debug)

        $this.Port = 0
    }

    hidden [string] GetNginxConfigPath() {
        $worktreePortsDir = Join-Path $this.ProjectPath "artifacts" "worktree-ports.d"
        return Join-Path $worktreePortsDir "$($this.InstanceName).conf"
    }

    hidden [void] WriteNginxConfig([bool]$debug) {
        if ($this.IsMainProject) { return }

        $worktreePortsDir = Join-Path $this.ProjectPath "artifacts" "worktree-ports.d"
        if (-not (Test-Path $worktreePortsDir)) {
            New-Item -ItemType Directory -Path $worktreePortsDir -Force | Out-Null
        }

        $nginxConfPath = $this.GetNginxConfigPath()
        Set-Content -Path $nginxConfPath -Value "`"$($this.InstanceName)`" $($this.Port);"
        if ($debug) { Write-Host "[DEBUG] Wrote nginx port mapping: $nginxConfPath" }
    }

    hidden [void] RemoveNginxConfig([bool]$debug) {
        $nginxConfPath = $this.GetNginxConfigPath()
        if (Test-Path $nginxConfPath) {
            Remove-Item $nginxConfPath -Force
            if ($debug) { Write-Host "[DEBUG] Removed nginx port mapping: $nginxConfPath" }
        }
    }

    hidden [void] ReloadNginx([bool]$debug) {
        $nginxContainer = docker ps --filter "name=actual-chat-infra-nginx" --format "{{.Names}}" 2>$null | Select-Object -First 1
        if (-not $nginxContainer) {
            Write-Host "WARNING: nginx container not found — worktree routing may not work." -ForegroundColor Yellow
            return
        }

        docker exec $nginxContainer nginx -s reload 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            if ($debug) { Write-Host "[DEBUG] Reloaded nginx" }
        } else {
            # Reload can fail due to stale bind mounts; restart refreshes them
            if ($debug) { Write-Host "[DEBUG] nginx reload failed, restarting container" }
            docker restart $nginxContainer 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) {
                if ($debug) { Write-Host "[DEBUG] Restarted nginx" }
            } else {
                Write-Host "WARNING: nginx restart failed — worktree routing may not work." -ForegroundColor Yellow
            }
        }
    }

    hidden [void] AddHostsEntries([bool]$debug) {
        if (-not $this.Hostnames) { return }
        if ($debug) { Write-Host "[DEBUG] Adding hosts entries for: $($this.Hostnames -join ', ')" }
        Update-HostEntries -Hostnames $this.Hostnames -DetectIP | Out-Null
    }

    hidden [void] RemoveHostsEntries([bool]$debug) {
        if (-not $this.Hostnames) { return }
        if ($debug) { Write-Host "[DEBUG] Removing hosts entries for: $($this.Hostnames -join ', ')" }
        Remove-HostEntries -Hostnames $this.Hostnames
    }
}

# PulseAudio setup for voice mode in Docker
class PulseAudioSetup {
    [int]$Port = 4713

    [bool] IsRunning() {
        if ((Get-CurrentOS) -eq "Windows") {
            $listening = netstat -an | Select-String ":$($this.Port)\s+.*LISTENING"
            return $null -ne $listening
        } else {
            $listening = bash -c "lsof -i :$($this.Port) -sTCP:LISTEN 2>/dev/null || ss -tln 2>/dev/null | grep -q ':$($this.Port) '"
            return $LASTEXITCODE -eq 0 -and $listening
        }
    }

    [bool] WaitForStart([int]$maxWaitSeconds) {
        $waited = 0
        while (-not $this.IsRunning() -and $waited -lt ($maxWaitSeconds * 2)) {
            Start-Sleep -Milliseconds 500
            $waited++
        }
        return $this.IsRunning()
    }

    [bool] IsInstalled() {
        $os = Get-CurrentOS
        if ($os -eq "macOS") {
            return $null -ne (Get-Command "pulseaudio" -ErrorAction SilentlyContinue)
        } elseif ($os -eq "Windows") {
            return (Test-Path "$env:LOCALAPPDATA\PulseAudio\bin\pulseaudio.exe") -or
                   (Test-Path "$env:ProgramFiles\PulseAudio\bin\pulseaudio.exe")
        }
        return $false
    }

    [void] EnsureRunning() {
        if ($this.IsRunning() -or -not $this.IsInstalled()) { return }
        Write-Host "Starting PulseAudio for voice mode..." -ForegroundColor Cyan
        $this.Setup()
    }

    [void] Setup() {
        switch (Get-CurrentOS) {
            "macOS"   { $this.SetupMacOS() }
            "Windows" { $this.SetupWindows() }
            "Linux"   { $this.SetupLinux() }
            default   { Write-Host "Unsupported OS for audio setup" -ForegroundColor Red; exit 1 }
        }
    }

    hidden [void] SetupMacOS() {
        # Check if PulseAudio is installed
        if (-not (Get-Command "pulseaudio" -ErrorAction SilentlyContinue)) {
            Write-Host "PulseAudio is not installed. Installing via Homebrew..." -ForegroundColor Cyan
            & brew install pulseaudio
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Failed to install PulseAudio. Please install Homebrew first: https://brew.sh"
                exit 1
            }
        }

        # Check if already running
        if ($this.IsRunning()) {
            Write-Host "PulseAudio is already running on port $($this.Port)" -ForegroundColor Green
            return
        }

        # Create config directory
        $configDir = "$env:HOME/.pulse"
        if (-not (Test-Path $configDir)) {
            New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        }

        # Create config with TCP module
        $configFile = "$configDir/default.pa"
        $homebrewPrefix = if (Test-Path "/opt/homebrew") { "/opt/homebrew" } else { "/usr/local" }
        $defaultPaPath = "$homebrewPrefix/etc/pulse/default.pa"

        if (-not (Test-Path $configFile) -or -not (Select-String -Path $configFile -Pattern "module-native-protocol-tcp" -Quiet)) {
            Write-Host "Configuring PulseAudio for Docker connections..." -ForegroundColor Cyan
            $configContent = @"
.include $defaultPaPath
load-module module-native-protocol-tcp auth-ip-acl=127.0.0.1;192.168.65.0/24 auth-anonymous=1
"@
            Set-Content -Path $configFile -Value $configContent
            Write-Host "Created config: $configFile" -ForegroundColor Green
        }

        # Start daemon
        Write-Host "Starting PulseAudio daemon..." -ForegroundColor Cyan
        & pulseaudio --load=module-native-protocol-tcp --exit-idle-time=-1 --daemon 2>&1 | Out-Null

        if ($this.WaitForStart(10)) {
            Write-Host "PulseAudio started successfully on port $($this.Port)" -ForegroundColor Green
            Write-Host "`nVoice mode should now work in Docker. Run 'c' to start Claude." -ForegroundColor Cyan
            Write-Host "`nTo stop: pulseaudio --kill" -ForegroundColor DarkGray
        } else {
            Write-Host "Failed to start PulseAudio. Try manually:" -ForegroundColor Yellow
            Write-Host "  pulseaudio --load=module-native-protocol-tcp --exit-idle-time=-1 --daemon" -ForegroundColor White
        }
    }

    hidden [void] SetupWindows() {
        $portableDir = "$env:LOCALAPPDATA\PulseAudio"
        $legacyDir = "$env:ProgramFiles\PulseAudio"
        # Prefer portable location, fall back to legacy (exe installer) location
        $installDir = if (Test-Path "$portableDir\bin\pulseaudio.exe") { $portableDir }
            elseif (Test-Path "$legacyDir\bin\pulseaudio.exe") { $legacyDir }
            else { $portableDir }
        $exePath = "$installDir\bin\pulseaudio.exe"
        $configDir = "$env:APPDATA\PulseAudio"
        $configFile = "$configDir\default.pa"

        # Install if needed
        if (-not (Test-Path $exePath)) {
            Write-Host "PulseAudio is not installed. Downloading..." -ForegroundColor Cyan
            $zipUrl = "https://github.com/pgaskin/pulseaudio-win32/releases/download/v5/pulseaudio.zip"
            $zipPath = "$env:TEMP\pulseaudio.zip"
            try {
                Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
            } catch {
                Write-Error "Failed to download PulseAudio from $zipUrl"
                exit 1
            }
            # Zip contains a "pulseaudio/" root folder, so extract to parent directory
            Expand-Archive -Path $zipPath -DestinationPath (Split-Path $installDir) -Force
            Remove-Item $zipPath -ErrorAction SilentlyContinue

            if (-not (Test-Path $exePath)) {
                Write-Error "PulseAudio installation failed."
                exit 1
            }
            Write-Host "PulseAudio installed to $installDir" -ForegroundColor Green
        }

        # Check if already running
        if ($this.IsRunning()) {
            Write-Host "PulseAudio is already running on port $($this.Port)" -ForegroundColor Green
            return
        }

        # Create config directory
        if (-not (Test-Path $configDir)) {
            New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        }

        # Create config with TCP module
        if (-not (Test-Path $configFile) -or -not (Select-String -Path $configFile -Pattern "module-native-protocol-tcp" -Quiet)) {
            Write-Host "Configuring PulseAudio for Docker connections..." -ForegroundColor Cyan
            $configContent = @"
.include $installDir/etc/pulse/default.pa
load-module module-native-protocol-tcp auth-ip-acl=127.0.0.1;192.168.65.0/24 auth-anonymous=1
"@
            Set-Content -Path $configFile -Value $configContent
            Write-Host "Created config: $configFile" -ForegroundColor Green
        }

        # Start daemon
        Write-Host "Starting PulseAudio..." -ForegroundColor Cyan
        Start-Process -FilePath $exePath -ArgumentList "--exit-idle-time=-1", "-F", $configFile -WindowStyle Hidden

        if ($this.WaitForStart(10)) {
            Write-Host "PulseAudio started successfully on port $($this.Port)" -ForegroundColor Green
            Write-Host "`nVoice mode should now work in Docker. Run 'c' to start Claude." -ForegroundColor Cyan
            Write-Host "`nTo stop: taskkill /IM pulseaudio.exe" -ForegroundColor DarkGray
        } else {
            Write-Host "Failed to start PulseAudio. Try running manually:" -ForegroundColor Yellow
            Write-Host "  `"$exePath`" --exit-idle-time=-1 -F `"$configFile`"" -ForegroundColor White
        }
    }

    hidden [void] SetupLinux() {
        Write-Host "On Linux, PulseAudio/PipeWire should already be available." -ForegroundColor Yellow
        Write-Host "If voice mode doesn't work, ensure the TCP module is loaded:" -ForegroundColor Yellow
        Write-Host "  pactl load-module module-native-protocol-tcp auth-ip-acl=127.0.0.1 auth-anonymous=1" -ForegroundColor White
    }
}

# Update .env file with server configuration
# Preserves existing file structure (comments, ordering, unrelated variables).
# Only updates lines whose values changed and appends new variables at the end.
function Update-EnvFile {
    param(
        [string]$ProjectPath,
        [hashtable]$Variables,
        [switch]$Debug
    )

    $envFilePath = Join-Path $ProjectPath ".env"
    $remaining  = [System.Collections.Generic.Dictionary[string,string]]::new()
    foreach ($k in $Variables.Keys) { $remaining[$k] = $Variables[$k] }
    $lines      = @()
    $changed    = $false

    # Read existing file, updating matching lines in place
    if (Test-Path $envFilePath) {
        $lines = @(Get-Content $envFilePath | ForEach-Object {
            $line = $_
            if ($line.Trim() -and -not $line.TrimStart().StartsWith('#')) {
                $eqIndex = $line.IndexOf('=')
                if ($eqIndex -gt 0) {
                    $key = $line.Substring(0, $eqIndex)
                    if ($remaining.ContainsKey($key)) {
                        $newValue = $remaining[$key]
                        $null = $remaining.Remove($key)
                        $newLine = "$key=$newValue"
                        if ($newLine -ne $line) { $changed = $true }
                        return $newLine
                    }
                }
            }
            return $line
        })
    }

    # Append any variables that weren't already in the file
    foreach ($entry in $remaining.GetEnumerator() | Sort-Object Key) {
        $lines += "$($entry.Key)=$($entry.Value)"
        $changed = $true
    }

    if ($changed) {
        Set-Content -Path $envFilePath -Value $lines
        if ($Debug) { Write-Host "[DEBUG] Updated .env file: $envFilePath" }
    } elseif ($Debug) {
        Write-Host "[DEBUG] .env file unchanged: $envFilePath"
    }
}

# Handle rwt command: remove worktree and its configuration
if ($removeWorktreeSuffix) {
    $mainProjectPath = Join-Path $env:AC_ProjectRoot $projectName
    $worktreePath = Join-Path $env:AC_ProjectRoot "$projectName-$removeWorktreeSuffix"

    Write-Host "Removing worktree: $projectName-$removeWorktreeSuffix" -ForegroundColor Cyan

    # Stop server and Docker containers; then remove server config
    if (Test-Path (Join-Path $mainProjectPath "ActualChat.sln")) {
        $server = [WorktreeServer]::new($mainProjectPath, $removeWorktreeSuffix)

        # Kill Docker containers for this worktree
        $containerBaseName = "$($projectName.ToLower())-$($removeWorktreeSuffix.ToLower())"
        $existingContainers = @(docker ps -a --filter "label=worktree=$containerBaseName" --format "{{.ID}}`t{{.Names}}" 2>$null | Where-Object { $_ })
        if ($existingContainers.Count -gt 0) {
            foreach ($entry in $existingContainers) {
                $parts = $entry -split "`t"
                $cId = $parts[0]
                $cName = $parts[1]
                Write-Host "Removing container: $cName" -ForegroundColor Cyan
                docker rm -f $cId 2>$null | Out-Null
            }
            Write-Host "Docker containers removed" -ForegroundColor Green
        } elseif ($debugMode) {
            Write-Host "[DEBUG] No Docker containers found for worktree '$containerBaseName'"
        }

        $server.Unregister($debugMode)

        $worktreeEnvFile = Join-Path $worktreePath ".env"
        if (Test-Path $worktreeEnvFile) {
            Remove-Item $worktreeEnvFile -Force
            if ($debugMode) { Write-Host "[DEBUG] Removed worktree .env file" }
        }
    }

    # Remove git worktree and its branch
    if (Test-Path $worktreePath) {
        $originalLocation = Get-Location
        Set-Location $mainProjectPath
        try {
            # Get branch name before removing worktree
            $worktreeBranch = $null
            $worktreeListOutput = git worktree list --porcelain 2>&1
            $inTargetWorktree = $false
            foreach ($line in $worktreeListOutput -split "`n") {
                if ($line -match "^worktree (.+)$" -and $Matches[1] -eq $worktreePath) {
                    $inTargetWorktree = $true
                } elseif ($line -match "^worktree " -and $inTargetWorktree) {
                    break
                } elseif ($inTargetWorktree -and $line -match "^branch refs/heads/(.+)$") {
                    $worktreeBranch = $Matches[1]
                    break
                }
            }

            # Remove worktree
            git worktree remove $worktreePath --force 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Git worktree removed" -ForegroundColor Green
            } else {
                Write-Host "Warning: git worktree remove failed, you may need to remove manually" -ForegroundColor Yellow
            }

            # Delete local branch if found
            if ($worktreeBranch) {
                git branch -D $worktreeBranch 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "Local branch '$worktreeBranch' deleted" -ForegroundColor Green
                } else {
                    Write-Host "Warning: could not delete branch '$worktreeBranch'" -ForegroundColor Yellow
                }
            }
        } finally {
            Set-Location $originalLocation
        }
    } else {
        Write-Host "Worktree directory not found: $worktreePath" -ForegroundColor Yellow
    }

    Write-Host "Done" -ForegroundColor Green
    exit 0
}

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

    # Convert worktree git paths to relative so they work in Docker
    Convert-WorktreeToRelativePaths -WorktreePath $worktreePath -MainProjectPath $mainProjectPath -Debug:$debugMode

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

    # Convert worktree git paths to relative so they work in Docker
    $mainProjectPath = Join-Path $env:AC_ProjectRoot $projectName
    Convert-WorktreeToRelativePaths -WorktreePath $worktreePath -MainProjectPath $mainProjectPath -Debug:$debugMode

    # Update project info for the worktree
    $projectRoot  = $worktreePath
    $folderName   = "$projectName-$featureWorktreeSuffix"
    $worktree     = $featureWorktreeSuffix
    $relativePath = ""
    Set-Location $worktreePath
}

# Register server config and write .env file (ActualChat projects only)
$isActualChatProject = Test-Path (Join-Path $projectRoot "ActualChat.sln")
$serverConfig = $null
if ($isActualChatProject) {
    $mainProjectPath = if ($worktree -or $worktreeSuffix -or $featureWorktreeSuffix) {
        Join-Path $env:AC_ProjectRoot $projectName
    } else {
        $projectRoot
    }
    $server = [WorktreeServer]::new($mainProjectPath, $worktree)
    $serverConfig = $server.Register($debugMode)

    # Write server configuration to .env file in the worktree directory.
    # Uses .NET configuration names so they're automatically picked up by the server.
    # Skipped for the main project (dev instance) so its .env stays untouched.
    if ($serverConfig.InstanceName -ne "dev") {
        $envVarsToSave = @{
            "CoreSettings__Instance" = $serverConfig.InstanceName
            "HostSettings__BasePort" = "$($serverConfig.Port)"
            "HostSettings__BaseUri" = "https://$($serverConfig.InstanceName).local.voxt.ai"
        }
        Update-EnvFile -ProjectPath $projectRoot -Variables $envVarsToSave -Debug:$debugMode
    }
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

# Expose AC_GITHUB_TOKEN as GH_TOKEN so `gh` CLI picks it up automatically
if ($env:AC_GITHUB_TOKEN -and -not $env:GH_TOKEN) {
    $env:GH_TOKEN = $env:AC_GITHUB_TOKEN
}

# Shared helpers used by the `chrome` and `edge` modes below.
function Test-DebugPort {
    param([int]$Port)
    if ($currentOS -eq "Windows") {
        return $null -ne (netstat -an | Select-String ":$Port\s+.*LISTENING")
    }
    bash -c "lsof -i :$Port -sTCP:LISTEN 2>/dev/null || nc -z localhost $Port 2>/dev/null" | Out-Null
    return $LASTEXITCODE -eq 0
}

function Ensure-FirewallRule {
    param([int]$Port, [string]$BrowserName)
    if ($currentOS -ne "Windows") { return }
    $ruleName = "$BrowserName Remote Debugging (Claude) port $Port"
    netsh advfirewall firewall show rule name="$ruleName" 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { return }

    Write-Host "Creating firewall rule for port $Port..." -ForegroundColor Cyan
    $result = netsh advfirewall firewall add rule `
        name="$ruleName" `
        dir=in action=allow protocol=tcp `
        localport=$Port profile=private `
        description="Allow $BrowserName remote debugging connections from WSL/Docker" 2>&1
    if ($LASTEXITCODE -ne 0) {
        if ($result -match "requires elevation|Access is denied|administrator") {
            Write-Host ""
            Write-Host "Failed to create firewall rule - administrator privileges required." -ForegroundColor Yellow
            Write-Host "Run this in an elevated PowerShell, or re-run as Administrator:" -ForegroundColor Yellow
            Write-Host "  netsh advfirewall firewall add rule name=`"$ruleName`" dir=in action=allow protocol=tcp localport=$Port profile=private" -ForegroundColor White
            exit 1
        }
        Write-Host "Warning: Failed to create firewall rule for port $Port`: $result" -ForegroundColor Yellow
    }
}

# Launches one or more debug-enabled browser instances. Caller supplies the
# OS-specific executable path, the default and anonymous profile-dir bases,
# and the browser name (used for log/firewall messages).
function Start-DebugBrowsers {
    param(
        [string]$BrowserName,
        [string]$ExePath,
        [string]$DefaultProfileDir,
        [string]$AnonProfileBase,
        [int]   $StartPort,
        [int]   $Count,
        [bool]  $UseAnonymous,
        [string[]]$ExtraArgs = @()
    )
    Write-Host "$BrowserName path: $ExePath"
    for ($i = 0; $i -lt $Count; $i++) {
        $port = $StartPort + $i
        $profileDir = if ($UseAnonymous) { "$AnonProfileBase-$port" } else { $DefaultProfileDir }

        Ensure-FirewallRule -Port $port -BrowserName $BrowserName

        if (Test-DebugPort -Port $port) {
            Write-Host "$BrowserName already running on port $port — skipping" -ForegroundColor Yellow
            continue
        }

        $label = if ($UseAnonymous) { "anonymous" } else { "default" }
        Write-Host "Starting $BrowserName on port $port ($label profile: $profileDir)..." -ForegroundColor Cyan
        # Pull out our own meta-flag (`--fake-media`) before anything is
        # forwarded to the browser. If present, Chrome is launched with the
        # synthetic media-stream backend (mjpeg/wav fake camera + mic);
        # otherwise Chrome opens the real camera and microphone. Default is
        # REAL devices so screencast/voice testing on actual hardware works
        # without per-launch tweaking; the dev rig opts in by adding
        # `--fake-media`.
        $useFakeMedia = $false
        $forwardedArgs = @()
        foreach ($a in $ExtraArgs) {
            if ($a -eq "--fake-media") {
                $useFakeMedia = $true
            } else {
                $forwardedArgs += $a
            }
        }

        # Permission / capture policy for the debug profile:
        #   --disable-notifications              deny Notification API without prompting
        #                                        (the "Allow notifications?" popup blocks the UI otherwise)
        #   --use-fake-ui-for-media-stream       auto-accept mic/camera (no permission prompt) — kept
        #                                        in both modes so the test profile never blocks on a
        #                                        permission popup, regardless of fake vs real devices.
        #   --use-fake-device-for-media-stream   (--fake-media only) feed synthetic streams instead of
        #                                        real devices. Required for the --use-file-for-fake-*
        #                                        flags to take effect — without it Chrome uses real cam/mic.
        #   --use-file-for-fake-video-capture    (--fake-media only) feed mjpeg as the camera stream
        #   --use-file-for-fake-audio-capture    (--fake-media only) feed wav as the mic stream
        #   --auto-select-desktop-capture-source auto-pick a Voxt-titled window for getDisplayMedia
        #                                        (skips the share-screen picker; matches Voxt's page
        #                                        title — see <PageTitle>@CoreConstants.AppName).
        #                                        Tab-only is a separate flag if window-mode picks a
        #                                        sibling instance: --auto-select-tab-capture-source-by-title=Voxt
        #   --test-type                          quiet "controlled by automated test software" infobar
        $fakeVideo = Join-Path $ScriptDir "lib/data/test-video-1.mjpeg"
        $fakeAudio = Join-Path $ScriptDir "lib/data/test-audio-1.wav"
        # Pass the project URL as a positional arg so the browser opens it as
        # its first tab — otherwise an anonymous profile shows the "Sign in
        # to Chrome" / "Welcome to Edge" greeter and you have to navigate
        # manually.
        # Built-in flags first, caller's pass-through next, then the URL —
        # later flags override earlier ones, so user-supplied args win.
        # TEMP: dropped `--use-file-for-fake-video-capture=...mjpeg` — under
        # Chromium 147 the fake-device pipeline silently stops producing
        # frames after ~1 second of MJPEG content (verified: track stays
        # `live` but `<video>.currentTime` never advances and rVFC
        # never fires). Without the flag Chrome falls back to its
        # built-in synthetic moving-color-bars fake, which is supposed
        # to keep producing frames indefinitely.
        # If this works, the next step is to convert the test mjpeg
        # to Y4M and put the flag back with that file.
        $cmdArgs = @(
            "--remote-debugging-port=$port",
            "--remote-debugging-address=0.0.0.0",
            "--user-data-dir=`"$profileDir`"",
            "--remote-allow-origins=*",
            "--disable-notifications",
            "--use-fake-ui-for-media-stream",
            "--auto-select-desktop-capture-source=Voxt",
            "--test-type"
        )
        if ($useFakeMedia) {
            $cmdArgs += @(
                "--use-fake-device-for-media-stream",
                # "--use-file-for-fake-video-capture=`"$fakeVideo`"",
                "--use-file-for-fake-audio-capture=`"$fakeAudio`""
            )
            Write-Host "  media: fake (synthetic camera, $fakeAudio mic)" -ForegroundColor DarkGray
        } else {
            Write-Host "  media: real devices (pass --fake-media for synthetic)" -ForegroundColor DarkGray
        }
        $cmdArgs = $cmdArgs + $forwardedArgs + @("https://local.voxt.ai/")
        if ($forwardedArgs.Count -gt 0) {
            Write-Host "  extra args: $($forwardedArgs -join ' ')" -ForegroundColor DarkGray
        }
        Start-Process -FilePath $ExePath -ArgumentList $cmdArgs

        $maxWait = 30; $waited = 0; $printedWaiting = $false
        while (-not (Test-DebugPort -Port $port) -and $waited -lt $maxWait) {
            Start-Sleep -Seconds 1; $waited++
            if ($waited -gt 2 -and -not $printedWaiting) {
                Write-Host "  waiting for port $port`: " -NoNewline
                $printedWaiting = $true
            }
            if ($printedWaiting) { Write-Host "." -NoNewline }
        }
        if ($printedWaiting) { Write-Host "" }

        if (Test-DebugPort -Port $port) {
            Write-Host "  ready on port $port" -ForegroundColor Green
        } else {
            Write-Host "  timed out waiting for port $port" -ForegroundColor Yellow
        }
    }
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

        # Collect propagated env vars for WSL (same rules as Docker)
        $wslPropagatedParts = @()
        Get-ChildItem env: | ForEach-Object {
            $name  = $_.Name
            $value = $_.Value
            if ($name -match '__' -or
                $name -eq 'AC_GITHUB_TOKEN' -or
                $name -eq 'GH_TOKEN' -or
                $name -eq 'NPM_READ_TOKEN' -or
                $name -eq 'GOOGLE_CLOUD_PROJECT' -or
                $name -like 'ActualChat_*' -or
                $name -like 'ActualLab_*' -or
                $name -like 'Claude_*') {
                $escapedValue = $value -replace "'", "'\''"
                $wslPropagatedParts += "$name='$escapedValue'"
            }
        }

        # Build env vars for WSL (explicit vars after propagated ones to override)
        $wslProjectPath = ConvertTo-WSLPath $projectRoot
        $wslPropagatedString = $wslPropagatedParts -join ' '
        $wslEnvString = ("$wslPropagatedString AC_ProjectRoot='$wslProjectRoot' DISABLE_AUTOUPDATER=1 AC_ProjectPath='$wslProjectPath' AC_Worktree='$worktree'").Trim()

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
            $worktreeInfo = "Worktree: $worktree"
            if ($serverConfig) { $worktreeInfo += " (port: $($serverConfig.Port))" }
            Write-Host $worktreeInfo
        }

        $envVars = @{
            "AC_ProjectRoot"    = $env:AC_ProjectRoot
            "AC_ProjectPath"    = $env:AC_ProjectPath
            "AC_OS"             = $env:AC_OS
            "AC_Worktree"       = $env:AC_Worktree
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
        # macOS: warn if Docker Desktop is too old for --network host support
        if ($currentOS -eq "macOS") {
            $dockerVersion = docker version --format '{{.Server.Version}}' 2>$null
            if ($debugMode) { Write-Host "[DEBUG] Docker version: $dockerVersion" }
            if ($dockerVersion -and $dockerVersion -match "^(\d+)\.(\d+)") {
                $major = [int]$Matches[1]
                $minor = [int]$Matches[2]
                if ($major -lt 4 -or ($major -eq 4 -and $minor -lt 34)) {
                    Write-Host "WARNING: Docker Desktop 4.34+ is required for --network host on macOS." -ForegroundColor Yellow
                    Write-Host "         Current version: $dockerVersion. Host services may not be reachable." -ForegroundColor Yellow
                }
            }
        }

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

        # --renew: remove existing containers for this worktree before creating a new one
        if ($renewContainer) {
            $existingToRemove = @(docker ps -a --filter "label=worktree=$containerBaseName" --format "{{.ID}}`t{{.Names}}" 2>$null | Where-Object { $_ })
            if ($existingToRemove.Count -gt 0) {
                foreach ($entry in $existingToRemove) {
                    $parts = $entry -split "`t"
                    $cId = $parts[0]
                    $cName = $parts[1]
                    if ($dryRun) {
                        Write-Host "Would remove container: $cName ($cId)" -ForegroundColor Yellow
                    } else {
                        Write-Host "Removing container: $cName" -ForegroundColor Cyan
                        docker rm -f $cId 2>$null | Out-Null
                    }
                }
            } else {
                Write-Host "No existing containers to remove." -ForegroundColor DarkGray
            }
        }

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
        $projectEnvVars = @(
            "-e", "AC_ProjectPath=$dockerProjectPath",
            "-e", "AC_Worktree=$worktree"
        )

        # Collect environment variables to propagate:
        # - Variables with __ in their names (e.g., ChatSettings__OpenAIApiKey)
        # - AC_GITHUB_TOKEN, GH_TOKEN, NPM_READ_TOKEN, GOOGLE_CLOUD_PROJECT
        # - ActualChat_*, ActualLab_*, Claude_* variables
        $propagatedEnvVars = @()
        Get-ChildItem env: | ForEach-Object {
            $name  = $_.Name
            $value = $_.Value
            if ($name -match '__' -or
                $name -eq 'AC_GITHUB_TOKEN' -or
                $name -eq 'GH_TOKEN' -or
                $name -eq 'NPM_READ_TOKEN' -or
                $name -eq 'GOOGLE_CLOUD_PROJECT' -or
                $name -like 'ActualChat_*' -or
                $name -like 'ActualLab_*' -or
                $name -like 'Claude_*') {
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

        # Chrome DevTools MCP: pass debug port so the MCP wrapper script can resolve the host IP
        # Docker Desktop on Windows uses a VM, so localhost/127.0.0.1 won't reach the host.
        # Chrome rejects non-IP Host headers, so the wrapper resolves host.docker.internal to an IPv4 IP.

        # PulseAudio for voice mode: auto-start if installed but stopped
        [PulseAudioSetup]::new().EnsureRunning()
        $pulseServer = if ($currentOS -in "macOS", "Windows") { "tcp:host.docker.internal:4713" } else { "tcp:localhost:4713" }
        $audioEnvVars = @(
            "-e", "PULSE_SERVER=$pulseServer"
        )

        $dockerArgs += $volumeMounts + $propagatedEnvVars + $audioEnvVars + @(
            "-e", "ANTHROPIC_API_KEY=$env:ANTHROPIC_API_KEY"
            "-e", "DISABLE_AUTOUPDATER=1"
            "-e", "DOTNET_SYSTEM_NET_DISABLEIPV6=1"
            "-e", "AC_ProjectRoot=/proj"
            "-e", "AC_CHROME_DEBUG_PORT=$ChromeDebugPort"
        ) + $projectEnvVars

        $dockerArgs += @(
            "-w", $dockerWorkDir
            $imageName
            "pwsh", $dockerScriptPath
        ) + $dockerScriptArgs

        if ($dryRun) {
            # Build env vars hashtable for display
            $dockerEnvVars = @{
                "AC_ProjectRoot"    = "/proj"
                "AC_ProjectPath"    = $dockerProjectPath
                "AC_OS"             = "Linux in Docker"
                "AC_Worktree"       = $worktree
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

    "audio" {
        [PulseAudioSetup]::new().Setup()
    }

    "chrome" {
        if ($currentOS -eq "Windows") {
            $exePaths = @(
                "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
                "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
                "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
            )
            $defaultProfileDir = "$env:LOCALAPPDATA\Google\Chrome\Playwright"
            $anonProfileBase   = "$env:LOCALAPPDATA\Google\Chrome\Playwright-anon"
        } elseif ($currentOS -eq "macOS") {
            $exePaths = @("/Applications/Google Chrome.app/Contents/MacOS/Google Chrome")
            $defaultProfileDir = "$env:HOME/Library/Application Support/Google/Chrome Playwright"
            $anonProfileBase   = "$env:HOME/Library/Application Support/Google/Chrome Playwright-anon"
        } else {
            $exePaths = @(
                "/usr/bin/google-chrome", "/usr/bin/google-chrome-stable",
                "/usr/bin/chromium-browser", "/usr/bin/chromium"
            )
            $defaultProfileDir = "$env:HOME/.config/google-chrome-playwright"
            $anonProfileBase   = "$env:HOME/.config/google-chrome-playwright-anon"
        }
        $exePath = $exePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $exePath) {
            Write-Error "Chrome not found. Please install Google Chrome."
            exit 1
        }
        Start-DebugBrowsers `
            -BrowserName "Chrome" `
            -ExePath $exePath `
            -DefaultProfileDir $defaultProfileDir `
            -AnonProfileBase $anonProfileBase `
            -StartPort $ChromeDebugStartPort `
            -Count $ChromeInstanceCount `
            -UseAnonymous $ChromeUseAnonymousProfile `
            -ExtraArgs $ChromeExtraArgs
    }

    "edge" {
        if ($currentOS -eq "Windows") {
            $exePaths = @(
                "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
                "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
                "$env:LOCALAPPDATA\Microsoft\Edge\Application\msedge.exe"
            )
            $defaultProfileDir = "$env:LOCALAPPDATA\Microsoft\Edge\Playwright"
            $anonProfileBase   = "$env:LOCALAPPDATA\Microsoft\Edge\Playwright-anon"
        } elseif ($currentOS -eq "macOS") {
            $exePaths = @("/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge")
            $defaultProfileDir = "$env:HOME/Library/Application Support/Microsoft Edge Playwright"
            $anonProfileBase   = "$env:HOME/Library/Application Support/Microsoft Edge Playwright-anon"
        } else {
            $exePaths = @(
                "/usr/bin/microsoft-edge", "/usr/bin/microsoft-edge-stable",
                "/usr/bin/microsoft-edge-beta", "/usr/bin/microsoft-edge-dev"
            )
            $defaultProfileDir = "$env:HOME/.config/microsoft-edge-playwright"
            $anonProfileBase   = "$env:HOME/.config/microsoft-edge-playwright-anon"
        }
        $exePath = $exePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $exePath) {
            Write-Error "Microsoft Edge not found. Please install Microsoft Edge."
            exit 1
        }
        Start-DebugBrowsers `
            -BrowserName "Edge" `
            -ExePath $exePath `
            -DefaultProfileDir $defaultProfileDir `
            -AnonProfileBase $anonProfileBase `
            -StartPort $EdgeDebugStartPort `
            -Count $EdgeInstanceCount `
            -UseAnonymous $EdgeUseAnonymousProfile `
            -ExtraArgs $EdgeExtraArgs
    }
}
