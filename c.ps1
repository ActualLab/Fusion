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

# Find project root by traversing up from current directory
function Find-ProjectRoot {
    $checkDir = Get-Location
    while ($checkDir) {
        $dirName = Split-Path -Leaf $checkDir
        if ($Projects -contains $dirName) {
            $parentPath = Split-Path -Parent $checkDir
            # Normalize paths for comparison
            $normalizedParent = $parentPath.TrimEnd('\', '/').ToLower()
            $normalizedRoot = $env:AC_ProjectRoot.TrimEnd('\', '/').ToLower()
            if ($normalizedParent -eq $normalizedRoot) {
                return @{
                    ProjectName = $dirName
                    ProjectRoot = $checkDir.ToString()
                    RelativePath = (Get-Location).ToString().Substring($checkDir.ToString().Length)
                }
            }
        }
        $parent = Split-Path -Parent $checkDir
        if ($parent -eq $checkDir) { break }
        $checkDir = $parent
    }
    return $null
}

# Main logic
$currentOS = Get-CurrentOS
$mode = "docker"  # default mode
$dryRun = $false
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
    Write-Host "  build        Build Docker image for current project"
    Write-Host "  help         Show this help message"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  --dry-run    Show environment variables and command without executing"
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
    Write-Host "  AC_OS             OS/environment description"
    Write-Host "  AC_Project0Path   Full path to project 0"
    Write-Host "  AC_Project1Path   Full path to project 1"
    Write-Host "  AC_Project2Path   Full path to project 2"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  c                 Run Claude in Docker"
    Write-Host "  c --dry-run       Show what Docker would run"
    Write-Host "  c os              Run Claude on host OS"
    Write-Host "  c wsl             Run Claude in WSL"
    Write-Host "  c build           Build Docker image"
    Write-Host "  c --resume abc    Pass --resume abc to Claude"
    Write-Host ""
}

# Parse arguments
$argIndex = 0
if ($args.Count -gt 0) {
    switch ($args[0]) {
        "wsl" {
            $mode = "wsl"
            $argIndex = 1
        }
        "os" {
            $mode = "os"
            $argIndex = 1
        }
        "build" {
            $mode = "build"
            $argIndex = 1
        }
        { $_ -in "help", "-h", "--help", "-?" } {
            Show-Help
            exit 0
        }
    }
}

# Check for --dry-run in remaining args
if ($argIndex -lt $args.Count) {
    $remainingArgs = $args[$argIndex..($args.Count - 1)]
    if ($remainingArgs.Count -gt 0 -and $remainingArgs[0] -eq "--dry-run") {
        $dryRun = $true
        if ($remainingArgs.Count -gt 1) {
            $claudeArgs = $remainingArgs[1..($remainingArgs.Count - 1)]
        }
    } else {
        $claudeArgs = $remainingArgs
    }
}

# Find current project
$projectInfo = Find-ProjectRoot
if (-not $projectInfo) {
    Write-Error "Could not find a known project root."
    Write-Error "Known projects: $($Projects -join ', ')"
    Write-Error "Make sure you're inside a project directory under AC_ProjectRoot ($env:AC_ProjectRoot)"
    exit 1
}

$projectName = $projectInfo.ProjectName
$projectRoot = $projectInfo.ProjectRoot
$relativePath = $projectInfo.RelativePath -replace "\\", "/"

Write-Host "Project: $projectName"
Write-Host "Mode: $mode"
if ($dryRun) {
    Write-Host "Dry run: yes"
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
        $wslScriptPath = "$wslWorkDir/c.ps1"
        if ($relativePath) {
            $wslScriptPath = (ConvertTo-WSLPath $projectRoot) + "/c.ps1"
        } else {
            $wslScriptPath = "$wslWorkDir/c.ps1"
        }
        $wslScriptPath = (ConvertTo-WSLPath $projectRoot) + "/c.ps1"

        Write-Host "WSL Project Root: $wslProjectRoot"
        Write-Host "WSL Working Directory: $wslWorkDir"

        # Build args for the script running in WSL
        $wslArgs = @("os")
        if ($dryRun) { $wslArgs += "--dry-run" }
        $wslArgs += $claudeArgs

        # Build project path env vars for WSL
        $wslProjectPath = ConvertTo-WSLPath $projectRoot
        $wslProjectEnvVars = @("AC_ProjectPath='$wslProjectPath'")
        for ($i = 0; $i -lt $Projects.Count; $i++) {
            $winProjPath = Join-Path $env:AC_ProjectRoot $Projects[$i]
            $wslProjPath = ConvertTo-WSLPath $winProjPath
            $wslProjectEnvVars += "AC_Project${i}Path='$wslProjPath'"
        }
        $wslEnvString = (@("AC_ProjectRoot='$wslProjectRoot'") + $wslProjectEnvVars) -join ' '

        # Run this script in WSL with "os" argument
        $wslCommand = "cd '$wslWorkDir' && export $wslEnvString && pwsh '$wslScriptPath' $($wslArgs -join ' ')"

        if ($dryRun) {
            # Build env vars hashtable for display
            $wslEnvVars = @{
                "AC_ProjectRoot" = $wslProjectRoot
                "AC_Project" = $projectName
                "AC_ProjectPath" = $wslProjectPath
                "AC_OS" = "Linux on WSL"
            }
            for ($i = 0; $i -lt $Projects.Count; $i++) {
                $winProjPath = Join-Path $env:AC_ProjectRoot $Projects[$i]
                $wslEnvVars["AC_Project${i}Path"] = ConvertTo-WSLPath $winProjPath
            }

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
            Write-Host "  wsl bash -c `"$wslCommand`""
            Write-Host ""
        } else {
            wsl bash -c $wslCommand
        }
    }

    "os" {
        # Run Claude directly on the host OS
        $env:AC_Project = $projectName
        $env:AC_ProjectPath = $projectRoot

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

        $envVars = @{
            "AC_ProjectRoot" = $env:AC_ProjectRoot
            "AC_Project" = $env:AC_Project
            "AC_ProjectPath" = $env:AC_ProjectPath
            "AC_OS" = $env:AC_OS
        }
        for ($i = 0; $i -lt $Projects.Count; $i++) {
            $envVars["AC_Project${i}Path"] = [Environment]::GetEnvironmentVariable("AC_Project${i}Path")
        }

        # Only skip permissions in Docker (sandboxed environment)
        $allArgs = if ($currentOS -eq "Docker") {
            @("--dangerously-skip-permissions") + $claudeArgs
        } else {
            $claudeArgs
        }

        if ($dryRun) {
            Show-DryRun -EnvVars $envVars -Command "claude" -Arguments $allArgs -ModeName $env:AC_OS
        } else {
            & claude @allArgs
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

        # Calculate Docker working directory
        $dockerWorkDir = "/proj/$projectName$relativePath"
        $containerName = "claude-$($projectName.ToLower())"
        $dockerScriptPath = "/proj/$projectName/c.ps1"

        Write-Host "Container: $containerName"
        Write-Host "Docker Working Directory: $dockerWorkDir"

        # Build args for the script running in Docker
        $dockerScriptArgs = @("os")
        if ($dryRun) { $dockerScriptArgs += "--dry-run" }
        $dockerScriptArgs += $claudeArgs

        # Build project path env vars for Docker
        $dockerProjectPath = "/proj/$projectName"
        $projectEnvVars = @("-e", "AC_ProjectPath=$dockerProjectPath")
        for ($i = 0; $i -lt $Projects.Count; $i++) {
            $projectEnvVars += "-e"
            $projectEnvVars += "AC_Project${i}Path=/proj/$($Projects[$i])"
        }

        # Build docker run command - run this script with "os" argument inside container
        $dockerArgs = @(
            "run", "-it", "--rm"
        ) + $volumeMounts + @(
            "-e", "ANTHROPIC_API_KEY=$env:ANTHROPIC_API_KEY"
            "-e", "Claude_GeminiAPIKey=$env:Claude_GeminiAPIKey"
            "-e", "AC_ProjectRoot=/proj"
        ) + $projectEnvVars + @(
            "--network", "host"
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
            if ($currentOS -eq "Windows") {
                Write-Host "  wt docker $($dockerArgs -join ' ')"
            } else {
                Write-Host "  docker $($dockerArgs -join ' ')"
            }
            Write-Host ""
        } else {
            if ($currentOS -eq "Windows") {
                # Use Windows Terminal on Windows
                & wt docker @dockerArgs
            } else {
                & docker @dockerArgs
            }
        }
    }
}
