@echo off
dotnet run --project "%~dp0build" -c Release --no-launch-profile -- %*
exit /b %ERRORLEVEL%
