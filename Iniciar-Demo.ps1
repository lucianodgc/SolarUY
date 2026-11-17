[CmdletBinding()]
param(
    [switch]$AltaDisponibilidad,
    [switch]$ReiniciarDatos,
    [switch]$SinGuardarHistorial,
    [guid]$TecnicoId = [guid]::Empty
)

$ErrorActionPreference = 'Stop'
$raizProyecto = $PSScriptRoot
$proyectoWeb = Join-Path $raizProyecto 'SolarUY.Presentation/SolarUY.Presentation.csproj'

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
    & docker compose --project-directory $raizProyecto up -d --build
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar la aplicación balanceada.' }
    Write-Host 'SolarUY: http://localhost:18088. El simulador está incluido en Docker.'
    Write-Host 'Para el chatbot, Ollama del host debe escuchar en una interfaz accesible desde Docker y tener qwen3:8b instalado.'
    exit 0
}

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
elseif ($TecnicoId -ne [guid]::Empty) {
    throw 'TecnicoId requiere ReiniciarDatos. El arranque normal conserva las asignaciones.'
}

$argumentos = @('run', '--project', $proyectoWeb, '--launch-profile', 'http')
if ($SinGuardarHistorial) {
    $argumentos += @('--', '--RecoleccionLecturas:Habilitada=false')
    Write-Host 'Monitoreo en vivo habilitado. Guardado de historial: desactivado en este arranque.'
}
& dotnet @argumentos
exit $LASTEXITCODE
