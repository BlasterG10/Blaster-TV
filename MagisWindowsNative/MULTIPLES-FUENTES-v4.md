# Magis Windows Native — Fuentes múltiples v4

## Qué cambia
- Permite registrar varias URLs M3U/M3U8 a la vez.
- Permite agregar varios archivos M3U/M3U8 locales.
- Permite guardar varias conexiones Xtream independientes (servidor, usuario y contraseña por fuente).
- Las fuentes activas se combinan dentro de la misma guía de TV y catálogo.
- El filtro Servidor identifica de qué fuente procede cada canal/título.
- Cada fuente se puede activar, desactivar o eliminar.
- Actualizar fuentes vuelve a consultar todas las fuentes activas.
- Las series consultan episodios usando el cliente Xtream de su propia fuente.
- Las fuentes y sus secretos se guardan en el `catalog.json` cifrado con DPAPI del usuario Windows actual.

## Cómo probar
1. Extrae el ZIP en una carpeta nueva, sin sobrescribir tu versión que funciona.
2. Abre `MagisWindowsNative.csproj` en VS Code o Visual Studio.
3. Ejecuta `dotnet restore`.
4. Ejecuta `dotnet build -c Release`.
5. Ejecuta `dotnet run -c Release`.
6. Abre **Administrar fuentes**.
7. Añade dos o más listas M3U por URL y/o conexiones Xtream.
8. Deja activas las fuentes que quieras combinar y pulsa **Guardar fuentes**.
9. Comprueba **TV en vivo**, **Catálogo** y el filtro **Servidor**.

## Compatibilidad y límites
- Las listas M3U/M3U8 se interpretan como fuentes de TV en vivo; los catálogos de películas y series se consultan mediante las conexiones Xtream.
- Se necesita una conexión a Internet para descargar fuentes remotas.
- Algunas URLs pueden caducar o exigir permisos del proveedor; se conserva la fuente y se informa del error para poder actualizarla.
- Las claves/credenciales solo se guardan localmente; no se incluyen en el ZIP.
- No se compiló en este entorno porque no hay .NET SDK instalado. Debe compilarse y probarse en Windows.
