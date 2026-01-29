:<<BATCH
    dotnet build-server shutdown
    ./run-build.cmd clean
    rmdir /S /Q artifacts\bin
    rmdir /S /Q artifacts\build
    rmdir /S /Q artifacts\claude-docker
    rmdir /S /Q artifacts\docs
    rmdir /S /Q artifacts\obj
    rmdir /S /Q artifacts\out
    rmdir /S /Q artifacts\publish
    rmdir /S /Q artifacts\samples
    rmdir /S /Q artifacts\tests
    echo "Clean completed."
    exit /b
BATCH

#!/bin/sh
dotnet build-server shutdown
./run-build.cmd clean
rm -rf artifacts/bin
rm -rf artifacts/build
rm -rf artifacts/claude-docker
rm -rf artifacts/docs
rm -rf artifacts/obj
rm -rf artifacts/out
rm -rf artifacts/publish
rm -rf artifacts/samples
rm -rf artifacts/tests
echo "Clean completed."
