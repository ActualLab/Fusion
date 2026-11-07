@echo off
rem Runs the Publish workflow on GitHub; ts/Publish.ps1 is what it runs there
call "%~dp0..\build\Publish.cmd" ts
