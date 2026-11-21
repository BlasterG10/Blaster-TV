# Empaqueta el canal y lo instala en tu Roku (modo desarrollador) desde Windows.
# Uso (PowerShell, dentro de la carpeta RokuMagisTV):
#   .\instalar-en-roku.ps1 -RokuIp 192.168.1.50 -Password TU_CLAVE_DE_DESARROLLADOR
# La clave es la que pusiste al activar el modo desarrollador (usuario: rokudev).
param(
    [Parameter(Mandatory = $true)][string]$RokuIp,
    [Parameter(Mandatory = $true)][string]$Password
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$zip = Join-Path (Split-Path -Parent $here) "MagisTV-Roku.zip"
if (Test-Path $zip) { Remove-Item $zip }

# El manifest tiene que quedar en la raíz del zip (no dentro de una carpeta).
Compress-Archive -Path (Join-Path $here "manifest"), (Join-Path $here "source"), (Join-Path $here "components"), (Join-Path $here "images") -DestinationPath $zip
Write-Host "Paquete creado: $zip"

# curl.exe viene con Windows 10/11. El instalador web del Roku usa autenticación digest.
& curl.exe --fail --silent --show-error --user "rokudev:$Password" --digest `
    -F "mysubmit=Install" -F "archive=@$zip" "http://$RokuIp/plugin_install" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "No se pudo instalar. Revisa la IP, la clave y que el modo desarrollador esté activo." -ForegroundColor Red
    exit 1
}
Write-Host "Instalado. El canal MagisTV se abre solo en el Roku." -ForegroundColor Green
