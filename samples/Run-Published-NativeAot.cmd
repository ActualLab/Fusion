@echo off
dotnet publish NativeAot/NativeAot.csproj
"../artifacts/samples/publish/NativeAot/release/Samples.NativeAot.exe"
