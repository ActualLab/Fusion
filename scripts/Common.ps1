# Common PowerShell utilities for ActualChat scripts

function Get-CurrentOS {
    <#
    .SYNOPSIS
        Detects the current operating system/environment.
    .OUTPUTS
        One of: Windows, Docker, WSL, Linux, macOS, Unknown
    #>
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

function Get-LocalIP {
    <#
    .SYNOPSIS
        Detects the local LAN IP address (first non-localhost IPv4).
    #>
    $os = Get-CurrentOS
    $localIp = $null
    switch ($os) {
        "macOS" {
            $localIp = (ifconfig | Select-String 'inet (\d+\.\d+\.\d+\.\d+)' -AllMatches).Matches |
                ForEach-Object { $_.Groups[1].Value } |
                Where-Object { $_ -ne '127.0.0.1' } |
                Select-Object -First 1
        }
        "Windows" {
            $localIp = (Get-NetIPAddress -AddressFamily IPv4 |
                Where-Object { $_.IPAddress -ne '127.0.0.1' -and $_.PrefixOrigin -ne 'WellKnown' } |
                Select-Object -First 1).IPAddress
        }
        default {
            # Linux, Docker, WSL
            $localIp = (hostname -I 2>$null) -split ' ' | Where-Object { $_ } | Select-Object -First 1
            if (-not $localIp) {
                $localIp = (ip route get 1.1.1.1 2>$null | Select-String 'src (\d+\.\d+\.\d+\.\d+)').Matches.Groups[1].Value
            }
        }
    }
    return $localIp
}

function Set-EnvFileValue {
    <#
    .SYNOPSIS
        Sets a key=value in a .env file. Creates the file if it doesn't exist.
    .PARAMETER Path
        Path to the .env file.
    .PARAMETER Key
        The environment variable name.
    .PARAMETER Value
        The value to set.
    #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Value
    )

    $content = if (Test-Path $Path) { Get-Content $Path -Raw } else { "" }
    if (-not $content) { $content = "" }

    if ($content -match "(?m)^$Key=") {
        $content = $content -replace "(?m)^$Key=.*$", "$Key=$Value"
    } else {
        $content = $content.TrimEnd() + "`n$Key=$Value`n"
    }

    Set-Content $Path $content.Trim() -NoNewline
    Add-Content $Path ""  # ensure trailing newline
}

function Update-LocalIP {
    <#
    .SYNOPSIS
        Detects local IP and writes it to .env file.
    .PARAMETER EnvFile
        Path to .env file. Defaults to .env in current directory.
    .OUTPUTS
        The detected local IP address.
    #>
    param(
        [string]$EnvFile = (Join-Path (Get-Location).Path ".env")
    )

    $localIp = Get-LocalIP
    if (-not $localIp) {
        Write-Error "Could not detect local IP address"
        return $null
    }

    Set-EnvFileValue -Path $EnvFile -Key "LOCAL_IP" -Value $localIp
    Write-Host "Updated $EnvFile with LOCAL_IP=$localIp"
    return $localIp
}

function Get-HostsFilePath {
    <#
    .SYNOPSIS
        Returns the platform-specific hosts file path.
    #>
    if ((Get-CurrentOS) -eq "Windows") {
        return "$env:SystemRoot\System32\drivers\etc\hosts"
    } else {
        return "/etc/hosts"
    }
}

function Update-HostEntries {
    <#
    .SYNOPSIS
        Adds or updates entries in the hosts file.
    .PARAMETER Hostnames
        Array of hostnames to add/update.
    .PARAMETER IP
        The IP address for the hosts entries.
    .PARAMETER DetectIP
        If set, detects LAN IP automatically instead of using -IP.
    #>
    param(
        [Parameter(Mandatory)][string[]]$Hostnames,
        [string]$IP,
        [switch]$DetectIP
    )

    if ($DetectIP) {
        $IP = Get-LocalIP
        if (-not $IP) {
            Write-Error "Could not detect local IP address"
            return $null
        }
    } elseif (-not $IP) {
        $IP = "127.0.0.1"
    }

    $hostsFile = Get-HostsFilePath
    $hostsContent = Get-Content $hostsFile -ErrorAction SilentlyContinue
    if (-not $hostsContent) { $hostsContent = @() }

    $entriesToAdd = @()
    $needsUpdate = $false

    foreach ($hostname in $Hostnames) {
        $escapedHostname = [regex]::Escape($hostname)
        $existingLine = $hostsContent | Where-Object { $_ -match "^\s*[\d\.]+\s+.*$escapedHostname" }
        $newLine = "$IP  $hostname"

        if ($existingLine) {
            if ($existingLine -notmatch "^\s*$([regex]::Escape($IP))\s+") {
                $needsUpdate = $true
                $entriesToAdd += $newLine
            }
        } else {
            $entriesToAdd += $newLine
        }
    }

    if ($entriesToAdd.Count -eq 0) {
        Write-Host "Hosts entries already up-to-date"
        return $IP
    }

    # Build the PowerShell command to run with elevation
    $script = ""
    if ($needsUpdate) {
        $patterns = ($Hostnames | ForEach-Object { [regex]::Escape($_) }) -join '|'
        $script += "`$content = Get-Content '$hostsFile' | Where-Object { `$_ -notmatch '^\s*[\d\.]+\s+.*($patterns)' }; "
        $script += "Set-Content '$hostsFile' `$content -Force; "
    }
    $newEntries = $entriesToAdd -join "`n"
    $script += "Add-Content '$hostsFile' `"``n$newEntries`""

    if ((Get-CurrentOS) -eq "Windows") {
        try {
            Invoke-Expression $script
            Write-Host "Updated hosts file"
        } catch {
            Write-Host "Requesting elevation to update hosts file..."
            try {
                Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -Command $script" -Wait -ErrorAction Stop
                Write-Host "Updated hosts file (via UAC)"
            } catch {
                Write-Host "Could not update hosts file. Add manually:" -ForegroundColor Yellow
                $entriesToAdd | ForEach-Object { Write-Host $_ }
            }
        }
    } else {
        Write-Host "Updating hosts file (sudo required)..."
        $sudoScript = $script -replace "'", "'\\''"
        bash -c "sudo pwsh -NoProfile -c '$sudoScript'"
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Updated hosts file"
        } else {
            Write-Host "Could not update hosts file. Add manually:" -ForegroundColor Yellow
            $entriesToAdd | ForEach-Object { Write-Host $_ }
        }
    }

    return $IP
}

function Remove-HostEntries {
    <#
    .SYNOPSIS
        Removes entries from the hosts file by hostname.
    .PARAMETER Hostnames
        Array of hostnames to remove.
    #>
    param(
        [Parameter(Mandatory)][string[]]$Hostnames
    )

    $hostsFile = Get-HostsFilePath
    $hostsContent = Get-Content $hostsFile -ErrorAction SilentlyContinue
    if (-not $hostsContent) { return }

    $newContent = $hostsContent | Where-Object {
        $line = $_
        $shouldKeep = $true
        foreach ($hostname in $Hostnames) {
            if ($line -match [regex]::Escape($hostname)) {
                $shouldKeep = $false
                break
            }
        }
        $shouldKeep
    }

    if ($newContent.Count -eq $hostsContent.Count) {
        Write-Host "No matching hosts entries found"
        return
    }

    if ((Get-CurrentOS) -eq "Windows") {
        try {
            Set-Content -Path $hostsFile -Value $newContent -ErrorAction Stop
            Write-Host "Removed hosts entries"
        } catch {
            Write-Host "Requesting elevation to update hosts file..."
            try {
                $tempFile = [System.IO.Path]::GetTempFileName()
                $newContent | Set-Content -Path $tempFile
                $script = "Copy-Item '$tempFile' '$hostsFile' -Force; Remove-Item '$tempFile'"
                Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -Command $script" -Wait -ErrorAction Stop
                Write-Host "Removed hosts entries (via UAC)"
            } catch {
                Write-Host "Could not update hosts file" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "Removing hosts entries (sudo required)..."
        $tempFile = [System.IO.Path]::GetTempFileName()
        $newContent | Set-Content -Path $tempFile
        bash -c "sudo cp '$tempFile' '$hostsFile' && rm '$tempFile'"
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Removed hosts entries"
        } else {
            Write-Host "Could not update hosts file" -ForegroundColor Yellow
            Remove-Item $tempFile -ErrorAction SilentlyContinue
        }
    }
}

function Get-ScriptDirectory {
    <#
    .SYNOPSIS
        Gets the directory containing the calling script.
    #>
    $dir = $PSScriptRoot
    if (-not $dir -and $PSCommandPath) { $dir = Split-Path -Parent $PSCommandPath }
    if (-not $dir) { $dir = (Get-Location).Path }
    return $dir
}
