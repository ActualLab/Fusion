@echo off
set DOTNET_ReadyToRun=0
set DOTNET_TieredPGO=1
set DOTNET_TC_QuickJitForLoops=1

set runtime=%1
if "%runtime%"=="" (
  set runtime=net8.0
)
shift
dotnet run --no-launch-profile -c:Release -f:%runtime% --project tests/ActualLab.Fusion.Tests.PerformanceTestRunner/ActualLab.Fusion.Tests.PerformanceTestRunner.csproj -- %*
