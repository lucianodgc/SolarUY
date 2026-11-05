[CmdletBinding()]
param(
    [switch]$ReiniciarDatos,
    [switch]$SinGuardarHistorial,
    [guid]$TecnicoId = [guid]::Empty
)

$ErrorActionPreference = 'Stop'
$raizProyecto = $PSScriptRoot
$proyectoWeb = Join-Path $raizProyecto 'SolarUY.Presentation/SolarUY.Presentation.csproj'

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
