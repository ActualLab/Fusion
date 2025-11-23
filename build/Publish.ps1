# build\Publish-NuGetPackages.ps1
# Publishes all .nupkg files from ../artifacts/nupkg to NuGet.org
# Assumes 'nuget.exe' is already in PATH (no download, no local copy)

$ErrorActionPreference = "Stop"

# Configuration
$ArtifactsPath       = Join-Path $PSScriptRoot "..\artifacts\nupkg"
$ApiKey              = $env:ActualChat_NuGet_API_Key
$NuGetSource         = "https://api.nuget.org/v3/index.json"
$MaxRetries          = 5
$InitialDelaySeconds = 3

# Validate API key
if (-not $ApiKey) {
    Write-Error "NUGET_API_KEY environment variable is not set!`nSet it in your CI or locally."
    exit 1
}

# Validate artifacts folder
if (-not (Test-Path $ArtifactsPath)) {
    Write-Error "Artifacts folder not found: $ArtifactsPath"
    exit 1
}

# Find packages
$packages = Get-ChildItem -Path $ArtifactsPath -Filter "*.nupkg" | Sort-Object Name

if ($packages.Count -eq 0) {
    Write-Host "No .nupkg files found in $ArtifactsPath" -ForegroundColor Yellow
    exit 0
}

Write-Host "`nFound $($packages.Count) package(s) to publish.`n" -ForegroundColor Cyan

$totalPublished = 0

foreach ($pkg in $packages) {
    $packageName = $pkg.Name
    $attempt = 0

    while ($true) {
        $attempt++
        $delay = $InitialDelaySeconds * [Math]::Pow(2, $attempt - 1)

        Write-Host "[Attempt $attempt/$MaxRetries] Pushing $packageName ..." -NoNewline

        try {
            nuget push $pkg.FullName $ApiKey -SkipDuplicate -NonInteractive -Source $NuGetSource

            if ($LASTEXITCODE -eq 0) {
                Write-Host " SUCCESS" -ForegroundColor Green
                $totalPublished++
                break
            }
            else {
                throw "nuget.exe exited with code $LASTEXITCODE"
            }
        }
        catch {
            Write-Host " FAILED" -ForegroundColor Red

            if ($attempt -ge $MaxRetries) {
                Write-Error "`nFailed to publish '$packageName' after $MaxRetries attempts."
                exit 1
            }

            Write-Host "   Retrying in $delay seconds... ($attempt/$MaxRetries)" -ForegroundColor Yellow
            Start-Sleep -Seconds $delay
        }
    }
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host "All done! Successfully published $totalPublished package(s) to NuGet.org" -ForegroundColor Green
Write-Host "=================================================================`n" -ForegroundColor Cyan
