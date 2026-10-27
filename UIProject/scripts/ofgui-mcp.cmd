@echo off
rem ---------------------------------------------------------------
rem OpenFairyGUI MCP server launcher (stdio transport).
rem
rem No machine-specific path is hard-coded here: Node.js and the
rem OpenFairyGUI install directory are resolved on this machine by
rem Find-Ofgui.ps1 (env vars / local config / PATH / auto-detect).
rem
rem cmd.exe is used on purpose: it forwards stdin/stdout handles to
rem node.exe unchanged, so the JSON-RPC stream stays byte-clean.
rem ---------------------------------------------------------------
setlocal EnableExtensions DisableDelayedExpansion

set "HERE=%~dp0"
set "NODE_EXE="
set "MCP_ENTRY="

for /f "usebackq tokens=1,2,3 delims=|" %%A in (`powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%HERE%Find-Ofgui.ps1" -EmitLaunch`) do (
  set "NODE_EXE=%%A"
  set "MCP_ENTRY=%%B"
)

if not defined NODE_EXE goto :notready
if not defined MCP_ENTRY goto :notready

rem Absolute allowed-root so the server works even if the MCP client
rem starts it with a different working directory. Leave a user-set
rem value untouched.
if not defined OPENFAIRYGUI_ALLOWED_PROJECT_ROOTS (
  for %%I in ("%HERE%..") do set "OPENFAIRYGUI_ALLOWED_PROJECT_ROOTS=%%~fI"
)

"%NODE_EXE%" "%MCP_ENTRY%"
set "EXITCODE=%ERRORLEVEL%"
endlocal & exit /b %EXITCODE%

:notready
echo [ofgui-mcp] OpenFairyGUI toolchain not ready: install Node.js ^>= 22 and OpenFairyGUI, 1>&2
echo [ofgui-mcp] then run: powershell -NoProfile -File scripts\Find-Ofgui.ps1 1>&2
endlocal & exit /b 3
