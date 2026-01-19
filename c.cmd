:<<"::CMDLITERAL"
@echo off
pwsh -ExecutionPolicy Bypass -File "%~dp0c.ps1" %*
exit /b %errorlevel%
::CMDLITERAL
exec pwsh "$(dirname "$0")/c.ps1" "$@"
