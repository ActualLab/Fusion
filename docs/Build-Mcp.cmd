@echo off
rem Builds everything the MCP server serves: the VitePress site (docs/.vitepress/dist),
rem the docs MCP index (docs/.generated/mcp-index.json), and the mcp-server dependencies.
rem Mirrors docs/mcp-server/Dockerfile (mdsnippets -> docs:build -> npm ci).
cd /d "%~dp0"
call Build-Site.cmd
cd /d "%~dp0mcp-server"
if not exist "node_modules" call npm install
