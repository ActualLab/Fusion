@echo off
rem Runs the Publish workflow on GitHub (NuGet + npm); ts/Publish.ps1 is what it runs for npm
call "%~dp0..\build\Publish.cmd"
