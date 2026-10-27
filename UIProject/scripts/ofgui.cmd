@echo off
rem ---------------------------------------------------------------
rem OpenFairyGUI CLI wrapper. Resolves Node.js + ofgui on this
rem machine instead of relying on a repository-relative Tools\ofgui.
rem
rem Usage (run inside UIProject):
rem   scripts\ofgui.cmd validate . --json
rem   scripts\ofgui.cmd inspect . --json
rem   scripts\ofgui.cmd doctor .
rem ---------------------------------------------------------------
setlocal EnableExtensions DisableDelayedExpansion

set "HERE=%~dp0"
set "NODE_EXE="
set "CLI_ENTRY="

for /f "usebackq tokens=1,2,3 delims=|" %%A in (`powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%HERE%Find-Ofgui.ps1" -EmitLaunch`) do (
  set "NODE_EXE=%%A"
  set "CLI_ENTRY=%%C"
)

if not defined CLI_ENTRY goto :notready

"%NODE_EXE%" "%CLI_ENTRY%" %*
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%

:notready
echo [ofgui] OpenFairyGUI toolchain not ready: install Node.js ^>= 22 and OpenFairyGUI, 1>&2
echo [ofgui] then run: powershell -NoProfile -File scripts\Find-Ofgui.ps1 1>&2
endlocal & exit /b 3
