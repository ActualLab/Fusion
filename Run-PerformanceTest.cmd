@echo off
rem Disables use of ReadToRun images / enables more aggressive optimization
rem set DOTNET_ReadyToRun=0

set runtime=%1
if "%runtime%"=="" (
  set runtime=net8.0
)
shift
dotnet run --no-launch-profile -p:UseMultitargeting=true -c:Release -f:%runtime% --project tests/ActualLab.Fusion.Tests.PerformanceTestRunner/ActualLab.Fusion.Tests.PerformanceTestRunner.csproj -- %*
