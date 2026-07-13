@echo off
REM Duplo-clique: garante o servico no ar, abre o tunel Cloudflare e o navegador.
REM Passe "build" como argumento para republicar o app antes (ex.: Start-ThreeDManager.cmd build).
setlocal
set "PS=%~dp0Start-ThreeDManager.ps1"

if /I "%~1"=="build" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS%" -Build
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS%"
)

endlocal
