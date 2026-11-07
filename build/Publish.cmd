@echo off
rem Usage: Publish.cmd [dotnet|ts|both]  (default: dotnet) - runs the Publish workflow on GitHub
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-PublishWorkflow.ps1" %*
