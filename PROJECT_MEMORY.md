

## 11. Asistente local de configuración — 2026-11-14

- **Objetivo:** simplificar la configuración de OMDb y el primer inicio de la aplicación para Windows sin pedir al usuario que comparta su clave por chat.
- **Archivos añadidos al ZIP de la aplicación:**
  - `Configurar-y-Ejecutar.ps1`: solicita la clave OMDb mediante entrada oculta, la guarda en la variable de entorno del usuario Windows (no en archivos del proyecto), comprueba la presencia de .NET 10 SDK, ejecuta restore/build y solo inicia la aplicación si compila.
  - `INICIAR-BLASTER-TV.bat`: lanzador de doble clic que ejecuta el asistente PowerShell con bypass limitado al proceso.
- **Archivo actualizado:** `README.md` con instrucciones del asistente.
- **Seguridad:** clave opcional; TVmaze continúa disponible sin OMDb. La clave se persiste en el entorno del usuario Windows y no se incluye en ZIP ni GitHub. No pedir ni almacenar la clave en la conversación.
- **Verificación:** ZIP creado en `/mnt/data/MagisWindowsNative_Autoconfig_OMDb_TVmaze.zip`; `unzip -t` confirmó que no hay errores en el archivo. No se ejecutó el script en Windows ni se compiló .NET en este entorno.
- **Limitación:** esto es un asistente local de configuración/compilación/inicio, no un MSI ni un EXE independiente firmado. Requiere .NET 10 SDK y conexión a NuGet.
- **Siguiente paso:** el usuario descarga, extrae y hace doble clic en `INICIAR-BLASTER-TV.bat`; si el SDK no está instalado, debe instalar .NET 10 SDK y volver a ejecutar.
