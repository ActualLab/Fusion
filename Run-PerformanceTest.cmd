@echo off
rem set DOTNET_ReadyToRun=0
rem set DOTNET_TieredPGO=1
rem set DOTNET_TC_QuickJitForLoops=1

set runtime=%1
if "%runtime%"=="" (
  set runtime=net8.0
)
shift
dotnet run --no-launch-profile -p:UseMultitargeting=true -c:Release -f:%runtime% --project tests/ActualLab.Fusion.Tests.PerformanceTestRunner/ActualLab.Fusion.Tests.PerformanceTestRunner.csproj -- %*
