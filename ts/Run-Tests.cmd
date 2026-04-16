@echo off
cd /d "%~dp0"
set CI=1
set NO_COLOR=1
npx vitest run --reporter=basic --silent %*
