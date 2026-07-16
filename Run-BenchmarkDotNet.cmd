@echo off
dotnet run --project tests/ActualLab.Fusion.Tests.BenchmarkRunner/ActualLab.Fusion.Tests.BenchmarkRunner.csproj -c Release -- %*
