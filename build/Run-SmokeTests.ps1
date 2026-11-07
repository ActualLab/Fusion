# Smoke tests: all TypeScript tests + the .NET tests marked with [Trait("Category", "Smoke")].
# "Run-Tests.cmd smoke" runs them; so does the Publish workflow before publishing anything.
# The TypeScript tests need "npm ci" (or "npm install") in ts/ to be done.
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot)
try {
    & ts/Run-Tests.cmd
    if ($LASTEXITCODE -ne 0) {
        throw "TypeScript tests failed."
    }

    foreach ($project in 'tests/ActualLab.Tests', 'tests/ActualLab.Fusion.Tests') {
        & dotnet test $project -c Release -f net11.0 --filter Category=Smoke
        if ($LASTEXITCODE -ne 0) {
            throw "Smoke tests failed in $project."
        }
    }
}
finally {
    Pop-Location
}
