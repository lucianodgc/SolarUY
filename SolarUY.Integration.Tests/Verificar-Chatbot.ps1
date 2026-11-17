[CmdletBinding()]
param(
    [switch]$OpenAI,
    [switch]$Ollama,
    [switch]$Completa,
    [switch]$Navegador,
    [switch]$SinCompilar,
    [ValidatePattern('^[A-Za-z0-9_-]+$')]
    [string]$Salida = 'Req017Verified',
    [ValidatePattern('^solaruy_req007_test_[A-Za-z0-9_]+$')]
    [string]$BaseDePruebas = 'solaruy_req007_test_req017',
    [string]$Filtro = 'FullyQualifiedName~Chatbot'
)

$ErrorActionPreference = 'Stop'
$req017Raiz = Split-Path -Parent $PSScriptRoot
$req017SecretsPath = Join-Path $env:APPDATA 'Microsoft/UserSecrets/solaruy-presentation-local/secrets.json'
if (-not (Test-Path -LiteralPath $req017SecretsPath)) { throw 'Configura primero los user-secrets de SolarUY.Presentation.' }
$req017Secrets = Get-Content -Encoding UTF8 -Raw -LiteralPath $req017SecretsPath | ConvertFrom-Json
$req017Source = if ($req017Secrets.'ConnectionStrings:SolarUY') {
    $req017Secrets.'ConnectionStrings:SolarUY'
} else { $req017Secrets.ConnectionStrings.SolarUY }
if ([string]::IsNullOrWhiteSpace($req017Source)) { throw 'Falta la conexion MariaDB en user-secrets.' }

# No se imprimen conexiones, contrasenas ni claves. Las variables viven solo
# durante este proceso y sus hijos. La base habitual nunca es el destino.
$req017Connection = New-Object System.Data.Common.DbConnectionStringBuilder
$req017Connection.set_ConnectionString($req017Source)
$req017Servidor = if ($req017Connection.ContainsKey('Server')) {
    [string]$req017Connection.get_Item('Server')
} else { [string]$req017Connection.get_Item('Host') }
if ($req017Servidor -notin @('127.0.0.1', 'localhost', '::1')) {
    throw 'El verificador automatico solo admite una instancia MariaDB local.'
}
$req017EnvPath = Join-Path $req017Raiz '.env'
if (Test-Path -LiteralPath $req017EnvPath) {
    $req017RootLine = Get-Content -Encoding UTF8 -LiteralPath $req017EnvPath |
        Where-Object { $_ -match '^MARIADB_ROOT_PASSWORD=' } | Select-Object -First 1
    if ($req017RootLine) {
        $req017Root = $req017RootLine.Substring($req017RootLine.IndexOf('=') + 1).Trim().Trim([char]34).Trim([char]39)
        foreach ($req017Alias in @('User', 'User ID', 'Uid', 'Username', 'Password', 'Pwd')) {
            [void]$req017Connection.Remove($req017Alias)
        }
        $req017Connection.set_Item('User ID', 'root')
        $req017Connection.set_Item('Password', $req017Root)
    }
}
$req017Connection.set_Item('Database', $BaseDePruebas)
# El README permite ampliar la preparación del millón de filas. Esto no
# modifica el umbral de cinco segundos medido por la prueba de rendimiento.
$req017Connection.set_Item('DefaultCommandTimeout', 120)
$env:SOLARUY_HISTORICO_TEST_CONNECTION = $req017Connection.get_ConnectionString()
$env:SOLARUY_CHATBOT_LIVE = if ($OpenAI) { '1' } else { '0' }
$env:SOLARUY_CHATBOT_OLLAMA = if ($Ollama) { '1' } else { '0' }
$env:SOLARUY_CHATBOT_UI = if ($Navegador) { '1' } else { '0' }
if ($OpenAI -and -not ($req017Secrets.'Chatbot:OpenAI:ApiKey' -or $req017Secrets.Chatbot.OpenAI.ApiKey -or $env:Chatbot__OpenAI__ApiKey)) {
    throw 'Falta la clave externa Chatbot:OpenAI:ApiKey para probar OpenAI.'
}
if (-not $Completa -and $Filtro -eq 'FullyQualifiedName~Chatbot') {
    if ($Ollama) { $Filtro = 'FullyQualifiedName~Chatbot_OllamaReal' }
    elseif ($OpenAI) { $Filtro = 'FullyQualifiedName~Chatbot_OpenAiReal' }
}

$req017Proyecto = if ($Completa) { Join-Path $req017Raiz 'SolarUY.sln' }
    else { Join-Path $PSScriptRoot 'SolarUY.Integration.Tests.csproj' }
$req017Argumentos = @('test', $req017Proyecto, '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false', "-p:BaseOutputPath=bin/$Salida/")
if ($SinCompilar) { $req017Argumentos += @('--no-build', '--no-restore') }
if (-not $Completa -or $Filtro -ne 'FullyQualifiedName~Chatbot') { $req017Argumentos += @('--filter', $Filtro) }
& dotnet @req017Argumentos
exit $LASTEXITCODE
