@echo off
setlocal
cd /d "%~dp0"
echo ============================================
echo  MAGIST TV - PUBLICACION LIGERA (x64)
echo ============================================
echo Esta version requiere .NET 10 Desktop Runtime.
echo Mantenga juntos el EXE, las DLL y la carpeta VLC.
echo.
dotnet publish MagisWindowsNative.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -p:UseAppHost=true -p:PublishTrimmed=false -o "%~dp0publish\MagisTV-Lite"
if errorlevel 1 (
  echo.
  echo ERROR: No se pudo publicar. Copie el error para revisarlo.
  pause
  exit /b 1
)
echo.
echo PUBLICACION COMPLETADA.
echo Abra: publish\MagisTV-Lite\MagisTV.exe
start "" explorer "%~dp0publish\MagisTV-Lite"
pause
endlocal
