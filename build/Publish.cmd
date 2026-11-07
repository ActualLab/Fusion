@echo off
rem Runs the Publish workflow on GitHub, which tests and publishes both NuGet and npm packages
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-PublishWorkflow.ps1"
