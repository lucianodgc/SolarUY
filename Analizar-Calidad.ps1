[CmdletBinding()]
param(
    [string]$Servidor = 'http://localhost:9000',
    [string]$Proyecto = 'SolarUY'
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:SONAR_TOKEN)) {
    throw 'Define SONAR_TOKEN con un token propio de SonarQube antes de ejecutar este script.'
}
$destino = [Uri]$Servidor
if (-not $destino.IsAbsoluteUri -or $destino.Scheme -notin @('http', 'https') -or $destino.UserInfo) {
    throw 'Servidor debe ser una URL HTTP/HTTPS sin credenciales.'
}
$estado = Invoke-RestMethod -Uri ($Servidor.TrimEnd('/') + '/api/system/status')
if ($estado.status -ne 'UP') { throw 'SonarQube todavía no está disponible.' }
$imagen = 'solaruy-scanner:local'
& docker build -t $imagen (Join-Path $PSScriptRoot 'deploy/sonar')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo construir el analizador.' }
$identificador = [Guid]::NewGuid().ToString('N')
$contenedor = 'solaruy-analisis-' + $identificador
$resultados = Join-Path $PSScriptRoot ('artifacts/calidad/' + $identificador)
[void](New-Item -ItemType Directory -Path $resultados -Force)
$servidorContenedor = [UriBuilder]::new($destino)
if ($destino.IsLoopback) { $servidorContenedor.Host = 'host.docker.internal' }
$servidorContenedor.Fragment = ''
$envAnterior = $env:SONAR_HOST_URL
$proyectoAnterior = $env:PROJECT_KEY
try {
    $env:SONAR_HOST_URL = $servidorContenedor.Uri.AbsoluteUri.TrimEnd('/')
    $env:PROJECT_KEY = $Proyecto
    & docker run --name $contenedor --entrypoint sh --add-host host.docker.internal:host-gateway `
        --mount "type=bind,source=$PSScriptRoot,target=/app,readonly" `
        --mount "type=volume,source=solaruy_scanner_nuget_app,target=/home/app/.nuget/packages" `
        -e SONAR_TOKEN -e SONAR_HOST_URL -e PROJECT_KEY `
        -e SOLARUY_HISTORICO_TEST_CONNECTION `
        $imagen /app/deploy/sonar/analizar.sh
    $estadoAnalisis = $LASTEXITCODE
    & docker cp "${contenedor}:/resultados/." $resultados
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron exportar los resultados del analizador.' }
    Write-Host "Resultados guardados en $resultados"
    if ($estadoAnalisis -ne 0) { throw 'El analisis o el Quality Gate no termino correctamente. Revisa la salida y el panel de SonarQube.' }
    Write-Host "Análisis publicado y Quality Gate aprobado: $Servidor/dashboard?id=$Proyecto"
}
finally {
    & docker rm -f $contenedor *> $null
    $env:SONAR_HOST_URL = $envAnterior
    $env:PROJECT_KEY = $proyectoAnterior
}
