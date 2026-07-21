@echo off
setlocal
rem Usage: Run-Tests.cmd [core|rpc|fusion|all] [fast|full]  (default: all fast)
set "SCOPE=%~1"
set "MODE=%~2"
if "%SCOPE%"=="" set "SCOPE=all"
if /i "%SCOPE%"=="fast" ( set "MODE=fast" & set "SCOPE=all" )
if /i "%SCOPE%"=="full" ( set "MODE=full" & set "SCOPE=all" )
if "%MODE%"=="" set "MODE=fast"
dotnet run --project "%~dp0build" -c Release --no-launch-profile -- test --test-scope %SCOPE% --test-mode %MODE%
exit /b %ERRORLEVEL%
