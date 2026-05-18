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

# Interface name patterns excluded from LAN IPv4 selection. Covers VPN tunnels,
# Apple proprietary radios, Docker/Hyper-V/VM bridges, and loopback aliases.
# Match is case-insensitive (PowerShell -match default).
$script:NonLanInterfaceRegex = '^(utun|tun|tap|ppp|wg|tailscale|zt|gif|stf|ipsec|awdl|llw|anpi|bridge|vmnet|docker|veth|br-|vboxnet|vmware|vethernet|loopback)'

function Select-LanIPv4 {
    <#
    .SYNOPSIS
        Picks the most likely LAN-reachable IPv4 from candidate (interface, address) pairs.
    .DESCRIPTION
        Filters out loopback (127/8), link-local (169.254/16), and addresses on
        virtual/tunnel/bridge interfaces (VPN, Docker, Hyper-V, etc.). Then
        prefers RFC1918 private ranges in this order: 192.168.0.0/16,
        10.0.0.0/8, 172.16.0.0/12. Falls through to the first remaining
        candidate (e.g. a public IP) if no RFC1918 match.
    .PARAMETER Candidates
        Array of objects with .Interface and .IPAddress properties.
    #>
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Candidates)

    $filtered = @($Candidates | Where-Object {
        $_ -and $_.IPAddress -and $_.Interface -and
        $_.IPAddress -match '^\d+\.\d+\.\d+\.\d+$' -and
        $_.IPAddress -notmatch '^127\.' -and
        $_.IPAddress -notmatch '^169\.254\.' -and
        $_.Interface -notmatch $script:NonLanInterfaceRegex
    })
    if ($filtered.Count -eq 0) { return $null }

    foreach ($tier in @('^192\.168\.', '^10\.', '^172\.(1[6-9]|2[0-9]|3[0-1])\.')) {
        $pick = $filtered | Where-Object { $_.IPAddress -match $tier } | Select-Object -First 1
        if ($pick) { return $pick.IPAddress }
    }
    return $filtered[0].IPAddress
}

function _Get-LocalIPv4Candidates {
    <#
    .SYNOPSIS
        Enumerates all (interface, IPv4) pairs on the host as PSCustomObjects.
    .NOTES
        Internal helper for Get-LocalIP. The leading underscore signals
        "private by convention" - PowerShell .ps1 files have no real
        visibility control without converting to a .psm1 module, so this is
        the lightweight marker. Do not call from outside Common.ps1.
    #>
    switch (Get-CurrentOS) {
        "macOS" {
            $pairs = @()
            $currentIface = $null
            foreach ($line in (ifconfig 2>$null)) {
                if ($line -match '^([^\s:]+):\s') {
                    $currentIface = $matches[1]
                } elseif ($currentIface -and $line -match '^\s+inet (\d+\.\d+\.\d+\.\d+)') {
                    $pairs += [pscustomobject]@{ Interface = $currentIface; IPAddress = $matches[1] }
                }
            }
            return $pairs
        }
        "Windows" {
            return @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                Where-Object { $_.PrefixOrigin -ne 'WellKnown' } |
                ForEach-Object {
                    [pscustomobject]@{ Interface = $_.InterfaceAlias; IPAddress = $_.IPAddress }
                })
        }
        default {
            # Linux, Docker, WSL
            $pairs = @()
            foreach ($line in (ip -4 -o addr show 2>$null)) {
                if ($line -match '^\d+:\s+(\S+)\s+inet\s+(\d+\.\d+\.\d+\.\d+)') {
                    $pairs += [pscustomobject]@{ Interface = $matches[1]; IPAddress = $matches[2] }
                }
            }
            return $pairs
        }
    }
}

function Get-LocalIP {
    <#
    .SYNOPSIS
        Detects a stable LAN IPv4 reachable from the host, Docker containers,
        and other devices on the same LAN.
    .DESCRIPTION
        Enumerates all IPv4 addresses on physical interfaces (excluding
        loopback, link-local, VPN tunnels, and Docker/VM bridges) and picks
        an RFC1918 private address, preferring 192.168/16, then 10/8, then
        172.16/12. Returns $null if no LAN candidates are found.
    #>
    return Select-LanIPv4 -Candidates (_Get-LocalIPv4Candidates)
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
        $existingLine = $hostsContent | Where-Object { $_ -match "(?<=\s)$escapedHostname(?=\s|$)" }
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

    $existingLines = @(Get-Content $hostsFile -ErrorAction SilentlyContinue)
    if ($needsUpdate) {
        $patterns = ($Hostnames | ForEach-Object { [regex]::Escape($_) }) -join '|'
        $existingLines = @($existingLines | Where-Object { $_ -notmatch "(?<=\s)($patterns)(?=\s|`$)" })
    }
    $newContent = ($existingLines + $entriesToAdd) -join "`n"

    # Write content to a temp file so the elevated script never needs to embed or
    # escape it — avoids all string-quoting and newline issues in the script body.
    $tempContent = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "update-hosts-content-$(Get-Random).txt")
    Set-Content $tempContent $newContent -NoNewline -Encoding ASCII

    # $ErrorActionPreference=Stop makes file-system errors terminating so the catch block can elevate.
    $script = "`$ErrorActionPreference='Stop'; Copy-Item '$tempContent' '$hostsFile' -Force"

    try {
        Invoke-Expression $script
        Write-Host "Updated hosts file"
    } catch {
        if ((Get-CurrentOS) -eq "Windows") {
            Write-Host "Requesting elevation to update hosts file..."
            $tempScript = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "update-hosts-$(Get-Random).ps1")
            try {
                Set-Content $tempScript $script -NoNewline -Encoding UTF8
                $proc = Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -File `"$tempScript`"" -Wait -PassThru -ErrorAction Stop
                if ($proc.ExitCode -eq 0) {
                    Write-Host "Updated hosts file (via UAC)"
                } else {
                    Write-Host "Could not update hosts file. Add manually:" -ForegroundColor Yellow
                    $entriesToAdd | ForEach-Object { Write-Host $_ }
                }
            } catch {
                Write-Host "Could not update hosts file. Add manually:" -ForegroundColor Yellow
                $entriesToAdd | ForEach-Object { Write-Host $_ }
            } finally {
                Remove-Item $tempScript -ErrorAction SilentlyContinue
            }
        } else {
            Write-Host "Updating hosts file (sudo required)..."
            $tempScript = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "update-hosts-$(Get-Random).ps1")
            try {
                Set-Content $tempScript $script -NoNewline
                sudo pwsh -NoProfile -File $tempScript
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "Updated hosts file (via sudo)"
                } else {
                    Write-Host "Could not update hosts file. Add manually:" -ForegroundColor Yellow
                    $entriesToAdd | ForEach-Object { Write-Host $_ }
                }
            } finally {
                Remove-Item $tempScript -ErrorAction SilentlyContinue
            }
        }
    } finally {
        Remove-Item $tempContent -ErrorAction SilentlyContinue
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
            if ($line -match "(?<=\s)$([regex]::Escape($hostname))(?=\s|$)") {
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
