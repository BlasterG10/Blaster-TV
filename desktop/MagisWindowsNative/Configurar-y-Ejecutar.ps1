$ErrorActionPreference = 'Stop'
$projectFile = Join-Path $PSScriptRoot 'MagisWindowsNative.csproj'

Write-Host ''
Write-Host '==============================================' -ForegroundColor Cyan
Write-Host '  BLASTER TV / MAGIS - CONFIGURACION INICIAL' -ForegroundColor Cyan
Write-Host '==============================================' -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path $projectFile)) {
    throw "No se encontro MagisWindowsNative.csproj en: $PSScriptRoot"
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host 'No se encontro .NET SDK.' -ForegroundColor Yellow
    Write-Host 'Instala .NET 10 SDK para Windows desde https://dotnet.microsoft.com/download/dotnet/10.0 y vuelve a ejecutar este archivo.'
    Read-Host 'Presiona Enter para cerrar'
    exit 2
}

$sdkText = (& dotnet --list-sdks 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $sdkText -notmatch '(^|\n)10\.') {
    Write-Host 'Se requiere .NET 10 SDK. Versiones detectadas:' -ForegroundColor Yellow
    Write-Host $sdkText
    Write-Host 'Instala .NET 10 SDK desde https://dotnet.microsoft.com/download/dotnet/10.0'
    Read-Host 'Presiona Enter para cerrar'
    exit 2
}

Write-Host 'La clave OMDb es opcional. Si no la tienes, pulsa Enter y podras usar la busqueda de series TVmaze.'
$secureKey = Read-Host 'Introduce tu clave OMDb (entrada oculta; no se guardara en archivos)' -AsSecureString
if ($secureKey.Length -gt 0) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    try {
        $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
        [Environment]::SetEnvironmentVariable('OMDB_API_KEY', $plainKey, 'User')
        $env:OMDB_API_KEY = $plainKey
        Write-Host 'Clave OMDb guardada en las variables de entorno de TU usuario.' -ForegroundColor Green
    }
    finally {
        if ($ptr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
        $plainKey = $null
        $secureKey = $null
    }
}
else {
    Write-Host 'Se omitio OMDb. TVmaze seguira disponible.' -ForegroundColor Yellow
}

Push-Location $PSScriptRoot
try {
    Write-Host ''
    Write-Host '[1/3] Restaurando paquetes NuGet...' -ForegroundColor Cyan
    & dotnet restore $projectFile
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore fallo.' }

    Write-Host ''
    Write-Host '[2/3] Compilando en Release...' -ForegroundColor Cyan
    & dotnet build $projectFile -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'La compilacion fallo. Copia el error y envialo para corregirlo.' }

    Write-Host ''
    Write-Host '[3/3] Iniciando la aplicacion...' -ForegroundColor Cyan
    & dotnet run --project $projectFile -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'La aplicacion se cerro con un codigo de error.' }
}
catch {
    Write-Host ''
    Write-Host $_ -ForegroundColor Red
    Write-Host 'No se modificaron los archivos de tus listas ni las credenciales IPTV.'
    Read-Host 'Presiona Enter para cerrar'
    exit 1
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host 'Proceso terminado.' -ForegroundColor Green
Read-Host 'Presiona Enter para cerrar'
