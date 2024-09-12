@echo off
rem Disables use of ReadToRun images / enables more aggressive optimization
rem set DOTNET_ReadyToRun=0

set runtime=%1
if "%runtime%"=="" (
  set runtime=net9.0
)
shift
dotnet build -p:UseMultitargeting=true -c:Release -f:%runtime% tests/ActualLab.Fusion.Tests.PerformanceTestRunner/ActualLab.Fusion.Tests.PerformanceTestRunner.csproj
"./artifacts/tests/bin/ActualLab.Fusion.Tests.PerformanceTestRunner/release_%runtime%/ActualLab.Fusion.Tests.PerformanceTestRunner.exe" %*

