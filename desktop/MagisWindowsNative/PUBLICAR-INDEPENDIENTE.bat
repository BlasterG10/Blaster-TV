@echo off
setlocal
cd /d "%~dp0"
echo ============================================
echo  MAGIST TV - PUBLICACION INDEPENDIENTE (x64)
echo ============================================
echo Esta version incluye .NET 10 y ocupa mas espacio.
echo Mantenga juntos el EXE, las DLL y la carpeta VLC.
echo.
dotnet publish MagisWindowsNative.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:UseAppHost=true -p:PublishTrimmed=false -o "%~dp0publish\MagisTV-Standalone"
if errorlevel 1 (
  echo.
  echo ERROR: No se pudo publicar. Copie el error para revisarlo.
  pause
  exit /b 1
)
echo.
echo PUBLICACION COMPLETADA.
echo Abra: publish\MagisTV-Standalone\MagisTV.exe
start "" explorer "%~dp0publish\MagisTV-Standalone"
pause
endlocal
