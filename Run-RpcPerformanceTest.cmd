@echo off
rem Disables use of ReadToRun images / enables more aggressive optimization
rem set DOTNET_ReadyToRun=0

set runtime=%1
if "%runtime%"=="" (
  set runtime=net10.0
)
shift
dotnet build -p:UseMultitargeting=true -c:Release -f:%runtime% tests/ActualLab.Fusion.Tests.RpcPerformanceTestRunner/ActualLab.Fusion.Tests.RpcPerformanceTestRunner.csproj
"./artifacts/tests/bin/ActualLab.Fusion.Tests.RpcPerformanceTestRunner/release_%runtime%/ActualLab.Fusion.Tests.RpcPerformanceTestRunner.exe" %*

