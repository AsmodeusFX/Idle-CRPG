@echo off
rem ---------------------------------------------------------------
rem Repeatable toolchain self-check:
rem   1) resolve Node.js + OpenFairyGUI on this machine
rem   2) start the MCP server through scripts\ofgui-mcp.cmd
rem   3) run initialize / tools/list over stdio
rem
rem Usage (inside UIProject): scripts\ofgui-verify.cmd
rem ---------------------------------------------------------------
setlocal EnableExtensions DisableDelayedExpansion

set "HERE=%~dp0"
set "NODE_EXE="

for /f "usebackq tokens=1,2,3 delims=|" %%A in (`powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%HERE%Find-Ofgui.ps1" -EmitLaunch`) do (
  set "NODE_EXE=%%A"
)

if not defined NODE_EXE goto :notready

"%NODE_EXE%" "%HERE%verify-ofgui-mcp.mjs" %*
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%

:notready
echo [ofgui-verify] OpenFairyGUI toolchain not ready: install Node.js ^>= 22 and OpenFairyGUI, 1>&2
echo [ofgui-verify] then run: powershell -NoProfile -File scripts\Find-Ofgui.ps1 1>&2
endlocal & exit /b 3
