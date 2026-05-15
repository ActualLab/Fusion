@echo off
cd /d "%~dp0"
if not exist "node_modules\.bin\vitepress.cmd" call npm install
call npm run slides:build
npm run docs:dev
