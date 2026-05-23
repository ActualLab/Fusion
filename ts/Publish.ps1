$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
$npmrcPath = Join-Path $PSScriptRoot ".npmrc"
try {
    # Check for npm key
    $npmKey = $env:ActualLab_NPM_Key
    if (-not $npmKey) {
        throw "ActualLab_NPM_Key environment variable is not set"
    }

    # Create temporary .npmrc with auth token
    "//registry.npmjs.org/:_authToken=$npmKey" | Set-Content $npmrcPath -NoNewline

    # Check npm auth and scope access before mutating package.json files or building
    $npmUser = & npm whoami --registry https://registry.npmjs.org/ 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "npm authentication failed: $npmUser"
    }
    Write-Host "npm user: $npmUser"

    $scopeAccess = & npm access list packages "@actuallab" --json --registry https://registry.npmjs.org/ 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "npm token can't access the '@actuallab' scope. Make sure the npm org exists and the token can create/publish packages under it. npm output: $scopeAccess"
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

    # Publish all workspace packages
    Write-Host "Publishing..."
    & npm publish --workspaces --access public
    $publishError = $LASTEXITCODE

    if ($publishError -ne 0) { throw "Publish failed" }

    Write-Host "Done! Published version $version"
}
finally {
    # Clean up temporary .npmrc
    Remove-Item $npmrcPath -ErrorAction SilentlyContinue

    # Restore package.json files
    & git checkout -- "packages/*/package.json" 2>$null
    Pop-Location
}
