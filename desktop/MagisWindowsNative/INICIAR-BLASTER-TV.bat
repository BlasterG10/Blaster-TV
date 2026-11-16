@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Configurar-y-Ejecutar.ps1"
if errorlevel 1 (
  echo.
  echo Hubo un problema. Revisa el mensaje anterior.
  pause
)
endlocal
