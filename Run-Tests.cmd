@echo off
setlocal
rem Usage: Run-Tests.cmd [core|rpc|fusion|all] [fast|full]  (default: all fast)
rem        Run-Tests.cmd smoke  - just the smoke tests (see build/Run-SmokeTests.ps1)
set "SCOPE=%~1"
set "MODE=%~2"
if /i "%SCOPE%"=="smoke" (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0build\Run-SmokeTests.ps1"
    exit /b %ERRORLEVEL%
)
if "%SCOPE%"=="" set "SCOPE=all"
if /i "%SCOPE%"=="fast" ( set "MODE=fast" & set "SCOPE=all" )
if /i "%SCOPE%"=="full" ( set "MODE=full" & set "SCOPE=all" )
if "%MODE%"=="" set "MODE=fast"
dotnet run --project "%~dp0build" -c Release --no-launch-profile -- test --test-scope %SCOPE% --test-mode %MODE%
exit /b %ERRORLEVEL%
