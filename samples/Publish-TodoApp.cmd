@echo off

set ASPNETCORE_ENVIRONMENT=Development
dotnet publish TodoApp/Host/Host.csproj -p:CanPublish=true -c:Debug