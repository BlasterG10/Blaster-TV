# MagisTV — logo, identidad visual y publicación Windows

## Identidad visual
- Logo MagisTV en PNG para la interfaz.
- Icono Windows multi-resolución (`Assets/MagisTV.ico`) para EXE, ventana y cuadros de diálogo.
- Paleta de marca violeta/magenta con fondos morado-negro.
- `AssemblyName` y nombre del ejecutable: `MagisTV.exe`.

## Publicar la versión más ligera
En Windows 10/11 x64 con .NET 10 SDK instalado:
1. Abra la carpeta del proyecto.
2. Ejecute `PUBLICAR-LIGERO.bat` o en PowerShell:

```powershell
dotnet restore
dotnet publish .\MagisWindowsNative.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -p:UseAppHost=true -p:PublishTrimmed=false -o .\publish\MagisTV-Lite
```

El EXE queda en `publish\MagisTV-Lite\MagisTV.exe`. Esta publicación es la más pequeña porque usa el .NET 10 Desktop Runtime instalado en el equipo.

## Publicar una versión para equipos sin .NET instalado
Ejecute `PUBLICAR-INDEPENDIENTE.bat` o:

```powershell
dotnet publish .\MagisWindowsNative.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:UseAppHost=true -p:PublishTrimmed=false -o .\publish\MagisTV-Standalone
```

Esta carpeta incluye el runtime de .NET y por eso ocupa más espacio. Es preferible para equipos donde no se puede instalar el runtime por separado.

## Importante sobre LibVLC
No se fuerza `PublishSingleFile=true` ni trimming. LibVLC usa DLL nativas y plugins; mantener el contenido publicado en una carpeta evita problemas de carga de `libvlc.dll` y su directorio `plugins`.

Para distribuir la aplicación, comprima la carpeta completa `MagisTV-Lite` o `MagisTV-Standalone`, no solamente `MagisTV.exe`. En el equipo de destino, el usuario abre `MagisTV.exe` normalmente.

## Validación
Compilar y publicar en Windows x64 con .NET 10 SDK. Probar apertura de la ventana, icono, reproducción VLC, M3U y Xtream en el equipo de destino. Este entorno no dispone del SDK .NET, por lo que no se afirma que la publicación ya haya sido compilada aquí.
