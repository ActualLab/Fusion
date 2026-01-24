#!/usr/bin/env pwsh
# Claude launcher script - runs Claude in Docker, WSL, or native OS

# Project definitions: folder name -> docker path suffix
# Can be overridden by AC_Project0, AC_Project1, etc. environment variables
$DefaultProjects = @(
    "ActualChat",
    "ActualLab.Fusion",
    "ActualLab.Fusion.Samples"
)

# Apply overrides from environment variables
$Projects = @()
for ($i = 0; $i -lt $DefaultProjects.Count; $i++) {
    $override = [Environment]::GetEnvironmentVariable("AC_Project$i")
    if ($override) {
        $Projects += $override
    } else {
        $Projects += $DefaultProjects[$i]
    }
}

# Check AC_ProjectRoot
if (-not $env:AC_ProjectRoot) {
    Write-Error "AC_ProjectRoot environment variable is not set."
    Write-Error "Please set it to your projects root directory (e.g., D:\Projects or /home/user/projects)"
    exit 1
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

# Find project root by traversing up from current directory
# Also detects worktrees named {ProjectName}-{Suffix}
function Find-ProjectRoot {
    param([switch]$Debug)

    $checkDir = (Get-Location).Path
    if ($Debug) { Write-Host "[DEBUG] Starting Find-ProjectRoot, checkDir=$checkDir" }

    while ($checkDir) {
        $dirName = Split-Path -Leaf $checkDir
        $parentPath = Split-Path -Parent $checkDir
        # Normalize paths for comparison
        $normalizedParent = if ($parentPath) { $parentPath.TrimEnd('\', '/').ToLower() } else { "" }
        $normalizedRoot = if ($env:AC_ProjectRoot) { $env:AC_ProjectRoot.TrimEnd('\', '/').ToLower() } else { "" }

        if ($Debug) {
            Write-Host "[DEBUG] dirName=$dirName, parentPath=$parentPath"
            Write-Host "[DEBUG] normalizedParent='$normalizedParent', normalizedRoot='$normalizedRoot'"
        }

        if ($normalizedParent -eq $normalizedRoot -and $normalizedParent -ne "") {
            # Check for exact project name match
            if ($Projects -contains $dirName) {
                $currentPath = (Get-Location).Path
                return @{
                    ProjectName = $dirName
                    ProjectRoot = $checkDir
                    RelativePath = $currentPath.Substring($checkDir.Length)
                    Worktree = ""
                }
            }
            # Check for worktree pattern: {ProjectName}-{Suffix}
            foreach ($proj in $Projects) {
                $pattern = '^' + [regex]::Escape($proj) + '-(.+)$'
                if ($Debug) { Write-Host "[DEBUG] Testing pattern '$pattern' against '$dirName'" }
                if ($dirName -match $pattern) {
                    $currentPath = (Get-Location).Path
                    if ($Debug) { Write-Host "[DEBUG] Match found! Worktree=$($Matches[1])" }
                    return @{
                        ProjectName = $proj
                        ProjectRoot = $checkDir
                        RelativePath = $currentPath.Substring($checkDir.Length)
                        Worktree = $Matches[1]
                    }
                }
            }
        }
        $parent = Split-Path -Parent $checkDir
        if (-not $parent -or $parent -eq $checkDir) { break }
        $checkDir = $parent
    }
    return $null
}

# Main logic
$currentOS = Get-CurrentOS
$mode = "docker"  # default mode
$fromMode = $null  # set when self-invoked (e.g., from-docker, from-wsl)
$worktreeSuffix = $null  # set when wt argument is used (regular worktree from current branch)
$featureWorktreeSuffix = $null  # set when fwt argument is used (feature branch worktree)
$dryRun = $false
$debugMode = $false
$claudeArgs = @()

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
    Write-Host "  chrome       Start Chrome with remote debugging enabled (for Playwright)"
    Write-Host "  build        Build Docker image for current project"
    Write-Host "  help         Show this help message"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  --dry-run    Show environment variables and command without executing"
    Write-Host "  --debug      Show debug output for troubleshooting"
    Write-Host ""
    Write-Host "Environment variables (required):"
    Write-Host "  AC_ProjectRoot    Root directory containing all projects"
    Write-Host ""
    Write-Host "Environment variables (optional overrides):"
    Write-Host "  AC_Project0       Override name for project 0 (default: $($DefaultProjects[0]))"
    Write-Host "  AC_Project1       Override name for project 1 (default: $($DefaultProjects[1]))"
    Write-Host "  AC_Project2       Override name for project 2 (default: $($DefaultProjects[2]))"
    Write-Host ""
    Write-Host "Environment variables set for Claude:"
    Write-Host "  AC_ProjectRoot    Project root path (adjusted for environment)"
    Write-Host "  AC_Project        Current project name"
    Write-Host "  AC_ProjectPath    Full path to current project (or worktree)"
    Write-Host "  AC_OS             OS/environment description"
    Write-Host "  AC_Worktree       Worktree suffix (empty if not in a worktree)"
    Write-Host "  AC_Project0Path   Full path to project 0"
    Write-Host "  AC_Project1Path   Full path to project 1"
    Write-Host "  AC_Project2Path   Full path to project 2"
    Write-Host ""
    Write-Host "Worktree support:"
    Write-Host "  Worktrees are automatically detected when the folder name is {Project}-{Suffix}"
    Write-Host "  Use wt to create a regular worktree from current branch (push/pull go to that branch)"
    Write-Host "  Use fwt to create a feature worktree with a new feat/<suffix> branch from origin/dev or origin/master"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  c                  Run Claude in Docker"
    Write-Host "  c --dry-run        Show what Docker would run"
    Write-Host "  c os               Run Claude on host OS"
    Write-Host "  c wsl              Run Claude in WSL"
    Write-Host "  c wt experiment    Run in worktree from current branch"
    Write-Host "  c fwt feature1     Run in worktree with feat/feature1 branch"
    Write-Host "  c os fwt bugfix    Run on host OS in feature worktree"
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

    # Check for fwt command (feature worktree with feat/<suffix> branch)
    if ($currentArg -eq "fwt") {
        $argIndex++
        if ($argIndex -lt $args.Count) {
            $featureWorktreeSuffix = $args[$argIndex]
            $argIndex++
        } else {
            Write-Error "The fwt command requires a worktree suffix argument"
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
    Write-Error "Could not find a known project root."
    Write-Error "Known projects: $($Projects -join ', ')"
    Write-Error "Make sure you're inside a project directory under AC_ProjectRoot ($env:AC_ProjectRoot)"
    exit 1
}

$projectName = $projectInfo.ProjectName
$projectRoot = $projectInfo.ProjectRoot
$relativePath = $projectInfo.RelativePath -replace "\\", "/"
$worktree = $projectInfo.Worktree

# Save the original project root and worktree (where c.ps1 was invoked from)
$originalProjectRoot = $projectRoot
$originalWorktree = $worktree

# Handle wt argument: create regular worktree from current branch and switch to it
if ($worktreeSuffix) {
    # Always use the main project path (not another worktree)
    $mainProjectPath = Join-Path $env:AC_ProjectRoot $projectName
    $worktreePath = Join-Path $env:AC_ProjectRoot "$projectName-$worktreeSuffix"

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

            # Create worktree from current branch (creates a new branch with same name as suffix, tracking current branch)
            # This makes push/pull work with the branch in the main project
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
    $projectRoot = $worktreePath
    $worktree = $worktreeSuffix
    $relativePath = ""
    Set-Location $worktreePath
}

# Handle fwt argument: create feature worktree with feat/<suffix> branch and switch to it
if ($featureWorktreeSuffix) {
    # Always use the main project path (not another worktree)
    $mainProjectPath = Join-Path $env:AC_ProjectRoot $projectName
    $worktreePath = Join-Path $env:AC_ProjectRoot "$projectName-$featureWorktreeSuffix"

    if (-not (Test-Path $worktreePath)) {
        Write-Host "Creating feature worktree: $projectName-$featureWorktreeSuffix"
        $originalLocation = Get-Location
        Set-Location $mainProjectPath
        try {
            # Determine base branch based on project
            $baseBranch = if ($projectName -eq "ActualChat") { "dev" } else { "master" }
            $featureBranch = "feat/$featureWorktreeSuffix"

            # Make sure we have the latest
            git fetch origin 2>$null

            # Check if the feature branch already exists (locally or remotely)
            $localBranchExists = git rev-parse --verify "refs/heads/$featureBranch" 2>$null
            $localExists = $LASTEXITCODE -eq 0
            $remoteBranchExists = git rev-parse --verify "refs/remotes/origin/$featureBranch" 2>$null
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
    $projectRoot = $worktreePath
    $worktree = $featureWorktreeSuffix
    $relativePath = ""
    Set-Location $worktreePath
}

# Suppress output when launching docker (inner instance will output)
if ($mode -ne "docker" -or $dryRun) {
    Write-Host "Project: $projectName"
    $displayMode = if ($fromMode) { $fromMode } else { $mode }
    Write-Host "Mode: $displayMode"
    if ($dryRun) {
        Write-Host "Dry run: yes"
    }
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
        $originalWslPath = ConvertTo-WSLPath $originalProjectRoot
        $wslScriptPath = "$originalWslPath/c.ps1"

        Write-Host "WSL Project Root: $wslProjectRoot"
        Write-Host "WSL Working Directory: $wslWorkDir"

        # Build args for the script running in WSL
        $wslArgs = @("os", "from-wsl")
        if ($dryRun) { $wslArgs += "--dry-run" }
        if ($debugMode) { $wslArgs += "--debug" }
        $wslArgs += $claudeArgs

        # Build project path env vars for WSL
        $wslProjectPath = ConvertTo-WSLPath $projectRoot
        $wslProjectEnvVars = @("AC_ProjectPath='$wslProjectPath'", "AC_Worktree='$worktree'")
        for ($i = 0; $i -lt $Projects.Count; $i++) {
            $winProjPath = Join-Path $env:AC_ProjectRoot $Projects[$i]
            $wslProjPath = ConvertTo-WSLPath $winProjPath
            $wslProjectEnvVars += "AC_Project${i}Path='$wslProjPath'"
        }
        $wslEnvString = (@("AC_ProjectRoot='$wslProjectRoot'") + $wslProjectEnvVars) -join ' '

        # Run this script in WSL with "os" argument
        # On Windows, we're already in wt (handled at script start)
        $wslCommandFull = "cd '$wslWorkDir' && export $wslEnvString && pwsh '$wslScriptPath' $($wslArgs -join ' ')"

        # Build env vars hashtable for display
        $wslEnvVars = @{
            "AC_ProjectRoot" = $wslProjectRoot
            "AC_Project" = $projectName
            "AC_ProjectPath" = $wslProjectPath
            "AC_OS" = "Linux on WSL"
            "AC_Worktree" = $worktree
        }
        for ($i = 0; $i -lt $Projects.Count; $i++) {
            $winProjPath = Join-Path $env:AC_ProjectRoot $Projects[$i]
            $wslEnvVars["AC_Project${i}Path"] = ConvertTo-WSLPath $winProjPath
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
        $env:AC_Project = $projectName
        $env:AC_ProjectPath = $projectRoot
        $env:AC_Worktree = $worktree

        # Set AC_OS based on detected environment
        $env:AC_OS = switch ($currentOS) {
            "Docker" { "Linux in Docker" }
            "WSL" { "Linux on WSL" }
            default { $currentOS }
        }

        # Set AC_ProjectXPath for all projects
        for ($i = 0; $i -lt $Projects.Count; $i++) {
            $projPath = Join-Path $env:AC_ProjectRoot $Projects[$i]
            [Environment]::SetEnvironmentVariable("AC_Project${i}Path", $projPath)
        }

        Write-Host "Running Claude on: $($env:AC_OS)"
        Write-Host "Working Directory: $(Get-Location)"
        if ($worktree) {
            Write-Host "Worktree: $worktree"
        }

        $envVars = @{
            "AC_ProjectRoot" = $env:AC_ProjectRoot
            "AC_Project" = $env:AC_Project
            "AC_ProjectPath" = $env:AC_ProjectPath
            "AC_OS" = $env:AC_OS
            "AC_Worktree" = $env:AC_Worktree
        }
        for ($i = 0; $i -lt $Projects.Count; $i++) {
            $envVars["AC_Project${i}Path"] = [Environment]::GetEnvironmentVariable("AC_Project${i}Path")
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
        # Build volume mounts for all projects
        $volumeMounts = @()
        foreach ($proj in $Projects) {
            $hostPath = Join-Path $env:AC_ProjectRoot $proj
            if ($currentOS -eq "Windows") {
                $hostPath = ConvertTo-DockerPath $hostPath
            }
            $containerPath = "/proj/$proj"
            $volumeMounts += "-v"
            $volumeMounts += "${hostPath}:${containerPath}"

            # Create and mount artifacts/claude-docker folder for Docker builds
            # This avoids permission conflicts with host artifacts folder
            $artifactsHostPath = Join-Path $env:AC_ProjectRoot $proj "artifacts" "claude-docker"
            if (-not (Test-Path $artifactsHostPath)) {
                New-Item -ItemType Directory -Path $artifactsHostPath -Force | Out-Null
            }
            if ($currentOS -eq "Windows") {
                $artifactsHostPath = ConvertTo-DockerPath $artifactsHostPath
            }
            $artifactsContainerPath = "/proj/$proj/artifacts"
            $volumeMounts += "-v"
            $volumeMounts += "${artifactsHostPath}:${artifactsContainerPath}"
        }

        # Add worktree mount if in a worktree
        # Note: The main project is already mounted above (e.g., /proj/ActualLab.Fusion)
        # This adds the worktree folder (e.g., /proj/ActualLab.Fusion-feature1)
        # Both are available so you can merge changes from worktree into main
        if ($worktree) {
            $worktreeHostPath = Join-Path $env:AC_ProjectRoot "$projectName-$worktree"
            if ($currentOS -eq "Windows") {
                $worktreeHostPath = ConvertTo-DockerPath $worktreeHostPath
            }
            $worktreeContainerPath = "/proj/$projectName-$worktree"
            $volumeMounts += "-v"
            $volumeMounts += "${worktreeHostPath}:${worktreeContainerPath}"
        }

        # Also mount the original worktree if different (for c.ps1 access when switching worktrees)
        if ($originalWorktree -and $originalWorktree -ne $worktree) {
            $origWorktreeHostPath = Join-Path $env:AC_ProjectRoot "$projectName-$originalWorktree"
            if ($currentOS -eq "Windows") {
                $origWorktreeHostPath = ConvertTo-DockerPath $origWorktreeHostPath
            }
            $origWorktreeContainerPath = "/proj/$projectName-$originalWorktree"
            $volumeMounts += "-v"
            $volumeMounts += "${origWorktreeHostPath}:${origWorktreeContainerPath}"
        }

        # Add Claude config mounts
        if ($currentOS -eq "Windows") {
            $volumeMounts += "-v"
            $volumeMounts += "$env:USERPROFILE/.claude:/home/claude/.claude"
            $volumeMounts += "-v"
            $volumeMounts += "$env:USERPROFILE/.claude.json:/home/claude/.claude.json"
        } else {
            $volumeMounts += "-v"
            $volumeMounts += "$env:HOME/.claude:/home/claude/.claude"
            $volumeMounts += "-v"
            $volumeMounts += "$env:HOME/.claude.json:/home/claude/.claude.json"
        }

        # Add gcloud config mount (maps user's gcloud config to container)
        $gcloudConfigPath = if ($currentOS -eq "Windows") {
            "$env:APPDATA/gcloud"
        } else {
            "$env:HOME/.config/gcloud"
        }
        if (Test-Path $gcloudConfigPath) {
            $volumeMounts += "-v"
            $volumeMounts += "${gcloudConfigPath}:/home/claude/.config/gcloud:ro"
        }

        # Add .gcp folder mount (for GOOGLE_APPLICATION_CREDENTIALS key file)
        $gcpKeyPath = if ($currentOS -eq "Windows") {
            "$env:USERPROFILE/.gcp"
        } else {
            "$env:HOME/.gcp"
        }
        if (Test-Path $gcpKeyPath) {
            $volumeMounts += "-v"
            $volumeMounts += "${gcpKeyPath}:/home/claude/.gcp:ro"
        }

        # Add .actual folder mount for ActualChat project (contains prompts and other config)
        if ($projectName -eq "ActualChat") {
            $actualPath = if ($currentOS -eq "Windows") {
                "$env:USERPROFILE/.actual"
            } else {
                "$env:HOME/.actual"
            }
            if (Test-Path $actualPath) {
                $volumeMounts += "-v"
                $volumeMounts += "${actualPath}:/home/claude/.actual:ro"
            }
        }

        # Calculate Docker working directory
        $dockerFolderName = if ($worktree) { "$projectName-$worktree" } else { $projectName }
        $dockerWorkDir = "/proj/$dockerFolderName$relativePath"
        $containerName = "claude-$($projectName.ToLower())"
        # Use c.ps1 from where it was originally invoked (could be main project or a worktree)
        $originalFolderName = Split-Path -Leaf $originalProjectRoot
        $dockerScriptPath = "/proj/$originalFolderName/c.ps1"

        if ($dryRun) {
            Write-Host "Container: $containerName"
            Write-Host "Docker Working Directory: $dockerWorkDir"
        }

        # Build args for the script running in Docker
        $dockerScriptArgs = @("os", "from-docker")
        if ($dryRun) { $dockerScriptArgs += "--dry-run" }
        if ($debugMode) { $dockerScriptArgs += "--debug" }
        $dockerScriptArgs += $claudeArgs

        # Build project path env vars for Docker
        # For worktrees, the docker path includes the worktree suffix
        $dockerProjectPath = if ($worktree) { "/proj/$projectName-$worktree" } else { "/proj/$projectName" }
        $projectEnvVars = @("-e", "AC_ProjectPath=$dockerProjectPath", "-e", "AC_Worktree=$worktree")
        for ($i = 0; $i -lt $Projects.Count; $i++) {
            $projectEnvVars += "-e"
            $projectEnvVars += "AC_Project${i}Path=/proj/$($Projects[$i])"
        }

        # Collect environment variables to propagate:
        # - Variables with __ in their names (e.g., ChatSettings__OpenAIApiKey)
        # - GITHUB_TOKEN, NPM_READ_TOKEN, GOOGLE_CLOUD_PROJECT
        # - ActualChat_* variables
        $propagatedEnvVars = @()
        Get-ChildItem env: | ForEach-Object {
            $name = $_.Name
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
        # This allows tests to connect to services (redis, postgres, nats) on host
        # AC_InClaudeDocker tells tests not to use docker-specific config
        $dockerArgs = @(
            "run", "-it", "--rm"
            "--network", "host"
        )

        $dockerArgs += $volumeMounts + @(
            "-e", "ANTHROPIC_API_KEY=$env:ANTHROPIC_API_KEY"
            "-e", "Claude_GeminiAPIKey=$env:Claude_GeminiAPIKey"
            "-e", "AC_ProjectRoot=/proj"
        ) + $projectEnvVars + $propagatedEnvVars

        $dockerArgs += @(
            "-w", $dockerWorkDir
            $containerName
            "pwsh", $dockerScriptPath
        ) + $dockerScriptArgs

        if ($dryRun) {
            # Build env vars hashtable for display
            $dockerEnvVars = @{
                "AC_ProjectRoot" = "/proj"
                "AC_Project" = $projectName
                "AC_ProjectPath" = $dockerProjectPath
                "AC_OS" = "Linux in Docker"
                "AC_Worktree" = $worktree
            }
            for ($i = 0; $i -lt $Projects.Count; $i++) {
                $dockerEnvVars["AC_Project${i}Path"] = "/proj/$($Projects[$i])"
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
        Start-Process -FilePath $chromePath -ArgumentList "--remote-debugging-port=$ChromeDebugPort", "--user-data-dir=`"$debugUserDataDir`"", "--remote-allow-origins=*"

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
