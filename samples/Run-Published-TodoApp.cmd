@echo off
dotnet publish TodoApp/Host/Host.csproj -p:CanPublish=true

set ASPNETCORE_ENVIRONMENT=Development
start /D ../artifacts/samples/publish/Host/release Samples.TodoApp.Host.exe 