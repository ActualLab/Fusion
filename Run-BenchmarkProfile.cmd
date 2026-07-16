@echo off
if "%~1"=="" goto usage
set "BENCHMARK_FILTER=%~1"
set "PROFILE_SECONDS=%~2"
set "PROFILE_DIR=BenchmarkDotNet.Artifacts\profiles"
set "PROFILE_PATH=%PROFILE_DIR%\%BENCHMARK_FILTER%-%RANDOM%.nettrace"
if not exist "%PROFILE_DIR%" mkdir "%PROFILE_DIR%"
dotnet build tests/ActualLab.Fusion.Tests.BenchmarkRunner/ActualLab.Fusion.Tests.BenchmarkRunner.csproj -c Release -m:1 /nr:false
if ERRORLEVEL 1 exit /b %ERRORLEVEL%
dotnet-trace collect --profile cpu-sampling --format Speedscope --output "%PROFILE_PATH%" --show-child-io -- dotnet artifacts/tests/bin/ActualLab.Fusion.Tests.BenchmarkRunner/release/ActualLab.Fusion.Tests.BenchmarkRunner.dll --profile "%BENCHMARK_FILTER%" %PROFILE_SECONDS%
exit /b %ERRORLEVEL%

:usage
echo Usage: Run-BenchmarkProfile.cmd BenchmarkName [seconds]
exit /b 2
