#!/bin/bash
# Disables use of ReadyToRun images / enables more aggressive optimization
# export DOTNET_ReadyToRun=0

runtime=$1
if [ -z "$runtime" ]; then
  runtime=net9.0
fi
shift

dotnet build -p:UseMultitargeting=true -c:Release -f:$runtime \
  tests/ActualLab.Fusion.Tests.PerformanceTestRunner/ActualLab.Fusion.Tests.PerformanceTestRunner.csproj
dotnet "./artifacts/tests/bin/ActualLab.Fusion.Tests.PerformanceTestRunner/release_$runtime/ActualLab.Fusion.Tests.PerformanceTestRunner.dll" "$@"
