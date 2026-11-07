# Publishes NuGet and/or npm packages by running .github/workflows/Publish.yml on GitHub,
# which authenticates via trusted publishing, so no API keys are needed here.
# The workflow publishes origin/master, so the local master must match it.
param(
    [ValidateSet('dotnet', 'ts', 'both')]
    [string] $Target = 'dotnet'
)

$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    $branch = & git rev-parse --abbrev-ref HEAD
    if ($branch -ne 'master') {
        throw "Publishing is possible only from master, but the current branch is '$branch'."
    }
    if (& git status --porcelain) {
        throw "The working tree has uncommitted changes."
    }

    & git fetch -q origin master
    $sha = & git rev-parse HEAD
    $remoteSha = & git rev-parse origin/master
    if ($sha -ne $remoteSha) {
        throw "master ($sha) doesn't match origin/master ($remoteSha): push or pull first."
    }

    $version = & dotnet nbgv get-version -v NuGetPackageVersion
    if ($LASTEXITCODE -ne 0 -or -not $version) {
        throw "Failed to get version from nbgv."
    }
    Write-Host "Publishing $version ($Target) from $sha"

    $startedAt = [DateTimeOffset]::UtcNow.AddSeconds(-10)
    & gh workflow run Publish.yml --ref master -f "target=$Target"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start the Publish workflow."
    }

    # "gh workflow run" doesn't report the run it starts, so it's found by its commit and start time
    $runId = $null
    for ($i = 0; $i -lt 30 -and -not $runId; $i++) {
        Start-Sleep -Seconds 2
        $runs = & gh run list --workflow Publish.yml --event workflow_dispatch --branch master --limit 10 `
            --json databaseId,headSha,createdAt | ConvertFrom-Json
        $runId = $runs |
            Where-Object { $_.headSha -eq $sha -and ([DateTimeOffset]$_.createdAt) -ge $startedAt } |
            Select-Object -First 1 -ExpandProperty databaseId
    }
    if (-not $runId) {
        throw "The started Publish workflow run isn't found."
    }

    & gh run watch $runId --exit-status --interval 15
    if ($LASTEXITCODE -ne 0) {
        & gh run view $runId --log-failed
        throw "The Publish workflow run failed: $(& gh run view $runId --json url --jq .url)"
    }

    Write-Host "Published $version ($Target)."
}
finally {
    Pop-Location
}
