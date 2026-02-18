$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    # Check for npm key
    $npmKey = $env:ActualLab_NPM_Key
    if (-not $npmKey) {
        throw "ActualLab_NPM_Key environment variable is not set"
    }

    # Get version from Nerdbank GitVersion
    $version = & dotnet nbgv get-version -v NuGetPackageVersion
    if ($LASTEXITCODE -ne 0 -or -not $version) {
        throw "Failed to get version from nbgv"
    }
    Write-Host "Publishing version: $version"

    # Update versions in all package.json files
    foreach ($file in Get-ChildItem "packages/*/package.json") {
        $json = Get-Content $file -Raw | ConvertFrom-Json
        $json.version = $version
        foreach ($depKey in @('dependencies', 'peerDependencies', 'devDependencies')) {
            $deps = $json.$depKey
            if ($deps) {
                foreach ($prop in @($deps.PSObject.Properties)) {
                    if ($prop.Name.StartsWith('@actuallab/')) {
                        $deps.$($prop.Name) = $version
                    }
                }
            }
        }
        $json | ConvertTo-Json -Depth 10 | Set-Content $file -NoNewline
        Write-Host "  Updated $file"
    }

    # Build all packages
    Write-Host "Building..."
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    # Create temporary .npmrc with auth token
    "//registry.npmjs.org/:_authToken=$npmKey" | Set-Content ".npmrc" -NoNewline

    # Publish all workspace packages
    Write-Host "Publishing..."
    & npm publish --workspaces --access public
    $publishError = $LASTEXITCODE

    # Clean up temporary .npmrc
    Remove-Item ".npmrc" -ErrorAction SilentlyContinue

    if ($publishError -ne 0) { throw "Publish failed" }

    Write-Host "Done! Published version $version"
}
finally {
    # Restore package.json files
    & git checkout -- "packages/*/package.json" 2>$null
    Pop-Location
}
