@echo off

set ASPNETCORE_ENVIRONMENT=Development
set ExePath=../artifacts/samples/bin/Host/debug/Samples.TodoApp.Host.dll

set ASPNETCORE_URLS=http://localhost:6005/
set Host__HostKind=BackendServer
set Host__Port=6005
start cmd /C dotnet %ExePath%
