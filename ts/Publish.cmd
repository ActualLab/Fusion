@echo off
cd /d "%~dp0"
pwsh -NoProfile -ExecutionPolicy Bypass -File Publish.ps1 %*
