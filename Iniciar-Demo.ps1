[CmdletBinding()]
param(
    [switch]$AltaDisponibilidad,
    [switch]$ReiniciarDatos,
    [switch]$SinGuardarHistorial,
    [guid]$TecnicoId = [guid]::Empty,
    [string]$OllamaRemoto
)

$ErrorActionPreference = 'Stop'
$raizProyecto = $PSScriptRoot
$proyectoWeb = Join-Path $raizProyecto 'SolarUY.Presentation/SolarUY.Presentation.csproj'

if ($PSBoundParameters.ContainsKey('OllamaRemoto')) {
    $destinoOllama = $null
    if (-not [Uri]::TryCreate($OllamaRemoto, [UriKind]::Absolute, [ref]$destinoOllama) -or
        $destinoOllama.Scheme -notin @('http', 'https') -or
        $destinoOllama.HostNameType -in @([UriHostNameType]::Unknown, [UriHostNameType]::Basic) -or
        $destinoOllama.UserInfo -or $destinoOllama.AbsolutePath -ne '/' -or
        $destinoOllama.Query -or $destinoOllama.Fragment) {
        throw 'OllamaRemoto requiere una URL base HTTP/HTTPS, sin credenciales, ruta API, consulta ni fragmento.'
    }
    $OllamaRemoto = $destinoOllama.AbsoluteUri
}

if ($AltaDisponibilidad) {
    if ($ReiniciarDatos -or $SinGuardarHistorial -or $TecnicoId -ne [guid]::Empty) {
        throw 'AltaDisponibilidad no admite restablecimiento de datos ni desactivar el historial.'
    }
    $archivoEnv = Join-Path $raizProyecto '.env'
    if (-not (Test-Path -LiteralPath $archivoEnv)) { throw 'Copia .env.example a .env y completa sus valores.' }
    $salidaCompose = & docker compose --project-directory $raizProyecto config --format json
    if ($LASTEXITCODE -ne 0) { throw 'Revisa las variables obligatorias de .env.' }
    $configuracionCompose = $salidaCompose | ConvertFrom-Json
    $baseCompose = $configuracionCompose.services.mariadb.environment
    $webCompose = $configuracionCompose.services.'solaruy-a'.environment
    $valores = @{
        MARIADB_DATABASE = $baseCompose.MARIADB_DATABASE
        MARIADB_USER = $baseCompose.MARIADB_USER
        MARIADB_PASSWORD = $baseCompose.MARIADB_PASSWORD
        SOLARUY_WEB_CLIENT_SECRET = $webCompose.Authentication__OpenIdConnect__ClientSecret
        SOLARUY_DIRECTORY_CLIENT_SECRET = $webCompose.Keycloak__Directory__ClientSecret
    }
    foreach ($nombre in @('MARIADB_DATABASE','MARIADB_USER','MARIADB_PASSWORD','SOLARUY_WEB_CLIENT_SECRET','SOLARUY_DIRECTORY_CLIENT_SECRET')) {
        if ([string]::IsNullOrWhiteSpace($valores[$nombre]) -or $valores[$nombre] -like 'REEMPLAZAR*') {
            throw "Completa $nombre en .env antes de iniciar."
        }
    }
    & docker compose --project-directory $raizProyecto up -d mariadb keycloak phpmyadmin
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar MariaDB y Keycloak.' }
    $lista = $false
    for ($intento = 0; $intento -lt 30; $intento++) {
        & docker compose --project-directory $raizProyecto exec -T mariadb healthcheck.sh --connect --innodb_initialized *> $null
        if ($LASTEXITCODE -eq 0) { $lista = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $lista) { throw 'MariaDB no quedó disponible.' }
    $anterior = $env:ConnectionStrings__SolarUY
    try {
        $conexion = New-Object System.Data.Common.DbConnectionStringBuilder
        $conexion['Server'] = '127.0.0.1'
        $conexion['Port'] = 3307
        $conexion['Database'] = $valores['MARIADB_DATABASE']
        $conexion['User'] = $valores['MARIADB_USER']
        $conexion['Password'] = $valores['MARIADB_PASSWORD']
        $env:ConnectionStrings__SolarUY = $conexion.ConnectionString
        & dotnet ef database update --project (Join-Path $raizProyecto 'SolarUY.Infrastructure') --startup-project $proyectoWeb -- --environment Development
        if ($LASTEXITCODE -ne 0) { throw 'No se pudieron aplicar las migraciones.' }
    }
    finally { $env:ConnectionStrings__SolarUY = $anterior }
    $endpointAnterior = $env:SOLARUY_OLLAMA_ENDPOINT
    $remotoAnterior = $env:SOLARUY_OLLAMA_PERMITIR_REMOTO
    try {
        if ($OllamaRemoto) {
            $env:SOLARUY_OLLAMA_ENDPOINT = $OllamaRemoto
            $env:SOLARUY_OLLAMA_PERMITIR_REMOTO = 'true'
        }
        & docker compose --project-directory $raizProyecto up -d --build
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar la aplicación balanceada.' }
    }
    finally {
        $env:SOLARUY_OLLAMA_ENDPOINT = $endpointAnterior
        $env:SOLARUY_OLLAMA_PERMITIR_REMOTO = $remotoAnterior
    }
    Write-Host 'SolarUY: http://localhost:18088. El simulador está incluido en Docker.'
    Write-Host 'Para el chatbot, Ollama debe ser accesible desde Docker y tener qwen3:8b instalado. Usa OllamaRemoto o .env para otra PC.'
    exit 0
}

# Arranque local: las dependencias se conservan en Docker y el simulador pertenece a esta ejecución.
function Test-PuertoLocal([int]$Puerto) {
    $escuchas = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    return @($escuchas | Where-Object Port -EQ $Puerto).Count -gt 0
}

if ($TecnicoId -ne [guid]::Empty -and -not $ReiniciarDatos) {
    throw 'TecnicoId requiere ReiniciarDatos. El arranque normal conserva las asignaciones.'
}
if (Test-PuertoLocal 5219) { throw 'El puerto 5219 está ocupado. Cierra la web local anterior antes de iniciar otra.' }
if (Test-PuertoLocal 5055) { throw 'El puerto 5055 está ocupado. Cierra el simulador local anterior antes de que el script lo inicie.' }
if (-not (Test-Path -LiteralPath (Join-Path $raizProyecto '.env'))) {
    throw 'Copia .env.example a .env y completa sus valores antes de iniciar las dependencias.'
}

& docker compose --project-directory $raizProyecto stop solaruy-a solaruy-b nginx simulador
if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar el modo local. Comprueba que Docker Desktop esté iniciado.' }
& docker compose --project-directory $raizProyecto up -d mariadb keycloak phpmyadmin
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron iniciar las dependencias. Revisa Docker Desktop y .env.' }

$baseLista = $false
for ($intento = 0; $intento -lt 30; $intento++) {
    & docker compose --project-directory $raizProyecto exec -T mariadb healthcheck.sh --connect --innodb_initialized *> $null
    if ($LASTEXITCODE -eq 0) { $baseLista = $true; break }
    Start-Sleep -Seconds 2
}
if (-not $baseLista) { throw 'MariaDB no quedó disponible.' }

$keycloakListo = $false
for ($intento = 0; $intento -lt 60; $intento++) {
    try {
        $oidc = Invoke-RestMethod -Uri 'http://127.0.0.1:8080/realms/solaruy/.well-known/openid-configuration' -TimeoutSec 3
        if ($oidc.issuer) { $keycloakListo = $true; break }
    }
    catch { }
    Start-Sleep -Seconds 2
}
if (-not $keycloakListo) { throw 'El realm solaruy de Keycloak no quedó disponible. Revisa docker compose logs keycloak.' }

$salidaLocal = & docker compose --project-directory $raizProyecto config --format json
if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer la configuración de MariaDB desde Compose.' }
$composeLocal = $salidaLocal | ConvertFrom-Json
$baseLocal = $composeLocal.services.mariadb.environment
$webLocal = $composeLocal.services.'solaruy-a'.environment
foreach ($secretoLocal in @($webLocal.Authentication__OpenIdConnect__ClientSecret, $webLocal.Keycloak__Directory__ClientSecret)) {
    if ([string]::IsNullOrWhiteSpace($secretoLocal) -or $secretoLocal -like 'REEMPLAZAR*') {
        throw 'Completa SOLARUY_WEB_CLIENT_SECRET y SOLARUY_DIRECTORY_CLIENT_SECRET en .env con los secretos actuales de Keycloak.'
    }
}
$conexionLocal = New-Object System.Data.Common.DbConnectionStringBuilder
$conexionLocal['Server'] = '127.0.0.1'
$conexionLocal['Port'] = 3307
$conexionLocal['Database'] = $baseLocal.MARIADB_DATABASE
$conexionLocal['User'] = $baseLocal.MARIADB_USER
$conexionLocal['Password'] = $baseLocal.MARIADB_PASSWORD
$variablesLocales = @{
    ConnectionStrings__SolarUY = $conexionLocal.ConnectionString
    Authentication__OpenIdConnect__Authority = 'http://localhost:8080/realms/solaruy'
    Authentication__OpenIdConnect__ClientId = $webLocal.Authentication__OpenIdConnect__ClientId
    Authentication__OpenIdConnect__ClientSecret = $webLocal.Authentication__OpenIdConnect__ClientSecret
    Keycloak__Directory__BaseUrl = 'http://localhost:8080'
    Keycloak__Directory__ClientSecret = $webLocal.Keycloak__Directory__ClientSecret
}
$variablesAnterioresLocales = @{}
foreach ($nombreLocal in $variablesLocales.Keys) {
    $variablesAnterioresLocales[$nombreLocal] = [Environment]::GetEnvironmentVariable($nombreLocal, 'Process')
}
try {
foreach ($nombreLocal in $variablesLocales.Keys) {
    [Environment]::SetEnvironmentVariable($nombreLocal, $variablesLocales[$nombreLocal], 'Process')
}
& dotnet ef database update --project (Join-Path $raizProyecto 'SolarUY.Infrastructure') --startup-project $proyectoWeb -- --environment Development
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron aplicar las migraciones usando MariaDB de Compose en 127.0.0.1:3307. Revisa el error anterior; cambiar .env no actualiza contraseñas de un volumen existente.' }

if ($ReiniciarDatos) {
    if (Get-Process -Name 'SolarUY.Presentation' -ErrorAction SilentlyContinue) {
        throw 'Cierra SolarUY con Ctrl + C antes de restablecer los datos.'
    }

    $archivoSql = Join-Path $raizProyecto 'solaruy_pruebas.sql'
    $contenidoSql = [System.IO.File]::ReadAllText($archivoSql, [System.Text.Encoding]::UTF8)
    if ($TecnicoId -ne [guid]::Empty) {
        $contenidoSql = "SET @TecnicoDemoId = '$($TecnicoId.ToString('D'))';`n" + $contenidoSql
    }

    Write-Host 'Restableciendo los datos de prueba en MARIADB_DATABASE del servicio mariadb de Docker Compose...'
    $codificacionAnterior = $OutputEncoding
    try {
        $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $contenidoSql | & docker compose --project-directory $raizProyecto exec -T mariadb sh -c 'export MYSQL_PWD="$MARIADB_PASSWORD"; exec mariadb --protocol=TCP -h127.0.0.1 -u"$MARIADB_USER" --default-character-set=utf8mb4 "$MARIADB_DATABASE"'
        if ($LASTEXITCODE -ne 0) {
            throw 'No se pudo importar el SQL. Revisa que MariaDB y sus credenciales sean correctos.'
        }
    }
    finally { $OutputEncoding = $codificacionAnterior }
    Write-Host 'Datos de ejemplo restaurados. Historial: 0 lecturas.'
}
$argumentos = @('run', '--project', $proyectoWeb, '--launch-profile', 'http')
$opcionesWeb = @('--InversorTcp:Host=127.0.0.1', '--InversorTcp:Port=5055')
if ($OllamaRemoto) {
    $opcionesWeb += @("--Chatbot:Ollama:Endpoint=$OllamaRemoto", '--Chatbot:Ollama:PermitirRemoto=true')
}
if ($SinGuardarHistorial) {
    $opcionesWeb += '--RecoleccionLecturas:Habilitada=false'
    Write-Host 'Monitoreo en vivo habilitado. Guardado de historial: desactivado en este arranque.'
}
if ($opcionesWeb.Count -gt 0) { $argumentos += @('--') + $opcionesWeb }

$proyectoSimulador = Join-Path $raizProyecto 'SolarUY.InversorSimulador/SolarUY.InversorSimulador.csproj'
& dotnet build $proyectoSimulador --configuration Debug
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el simulador.' }
$binarioSimulador = Join-Path $raizProyecto 'SolarUY.InversorSimulador/bin/Debug/net8.0/SolarUY.InversorSimulador.dll'
$directorioLogs = Join-Path $raizProyecto 'artifacts/demo'
[void](New-Item -ItemType Directory -Path $directorioLogs -Force)
$identificadorLog = [Guid]::NewGuid().ToString('N')
$logSalida = Join-Path $directorioLogs "simulador-$identificadorLog.log"
$logErrores = Join-Path $directorioLogs "simulador-$identificadorLog.error.log"
$simuladorLocal = $null
$codigoWeb = 0
try {
    $simuladorLocal = Start-Process -FilePath (Get-Command dotnet -CommandType Application).Source `
        -ArgumentList @("`"$binarioSimulador`"", '--InversorTcp:Host=127.0.0.1', '--InversorTcp:Port=5055') `
        -WorkingDirectory $raizProyecto -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $logSalida -RedirectStandardError $logErrores
    $simuladorListo = $false
    for ($intento = 0; $intento -lt 30; $intento++) {
        $simuladorLocal.Refresh()
        if ($simuladorLocal.HasExited) { throw "El simulador terminó antes de iniciar. Revisa $logErrores" }
        if (Test-PuertoLocal 5055) { $simuladorListo = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $simuladorListo) { throw "El simulador no abrió el puerto 5055. Revisa $logErrores" }
    Write-Host 'Dependencias listas. Web: http://localhost:5219. Simulador local: 127.0.0.1:5055.'
    Write-Host "Logs del simulador: $directorioLogs. Ctrl+C detiene la web y este simulador; Docker conserva los datos."
    & dotnet @argumentos
    $codigoWeb = $LASTEXITCODE
}
finally {
    if ($null -ne $simuladorLocal) {
        $simuladorLocal.Refresh()
        if (-not $simuladorLocal.HasExited) { Stop-Process -InputObject $simuladorLocal -ErrorAction SilentlyContinue }
        $simuladorLocal.Dispose()
    }
}
}
finally {
    foreach ($nombreLocal in $variablesAnterioresLocales.Keys) {
        [Environment]::SetEnvironmentVariable($nombreLocal, $variablesAnterioresLocales[$nombreLocal], 'Process')
    }
}
exit $codigoWeb
