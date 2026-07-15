@echo off
rem Starts the Fusion docs + MCP server at http://localhost:8080/mcp (POST).
rem Builds the VitePress site + MCP index first if they're missing (Build-Mcp.cmd).
rem Requires ripgrep (rg) on PATH for the source_* / symbol_search tools.
cd /d "%~dp0"
if not exist ".generated\mcp-index.json" goto build
if not exist "mcp-server\node_modules" goto build
goto run
:build
call "%~dp0Build-Mcp.cmd"
:run

rem Point the source_* / symbol_search tools (ripgrep) at this checkout's
rem real src, samples, and tests folders.
pushd "%~dp0.."
set "SOURCE_DIR=%CD%"
popd
set "SOURCE_ROOTS=src,samples,tests"
set "PORT=8080"

cd /d "%~dp0mcp-server"
npm start
