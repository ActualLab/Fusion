@echo off
rem Usage: Publish.cmd [-no-test]
rem Runs the Publish workflow on GitHub, which runs the smoke tests (unless -no-test) and publishes NuGet and npm packages
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-PublishWorkflow.ps1" %*
