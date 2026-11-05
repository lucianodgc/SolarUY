# SolarUY

SolarUY es una aplicación web para gestionar parques solares, nodos y mantenimiento de paneles. Permite consultar generación, ver reportes y operar un inversor simulado según el rol del usuario: Administrador, Técnico o Consulta.

Monitoreo y Simulador reciben datos por SignalR cada 2 segundos. El historial se guarda cada 30 segundos y alimenta los reportes.

## Tecnologías y organización

Se utiliza C# con .NET 8, Blazor Server y Radzen, EF Core con Pomelo y MariaDB 11.8, Keycloak con OpenID Connect, SignalR, TCP y Docker Compose. La aplicación se ejecuta en el servidor; no usa WebAssembly.

La solución sigue una arquitectura por capas:

| Proyecto | Responsabilidad |
| --- | --- |
| Presentation | Pantallas, autenticación y conexiones SignalR. |
| Application | Casos de uso, permisos y coordinación. |
| Domain | Entidades y reglas del negocio. |
| Infrastructure | Base de datos e integraciones externas. |
| InversorSimulador | Proceso separado que responde por TCP como un inversor. |

## Diagramas

Modelo de datos: los responsables se identifican mediante su usuario de Keycloak, sin una tabla local de usuarios.

<img width="988" height="737" alt="MER" src="https://github.com/user-attachments/assets/1869ff6a-645e-4b18-9eb6-f18ba0fb95f5" />

Arquitectura propuesta en la primera entrega. Actualmente se usa una instancia; el balanceo y el chat con IA quedan pendientes.

<img width="1204" height="804" alt="Arquitectura" src="https://github.com/user-attachments/assets/27bb301c-7e24-4ad8-b4e8-98c013fe0d0a" />

## Desarrollo con IA y SonarQube

Codex ayudó a implementar funcionalidades, explicar errores y revisar avisos de SonarQube. El proceso dejó como mejora pendiente reforzar la comprensión y revisión del código y mantener registros más breves. Los requisitos, especificaciones y resultados están en `prompting/`.

El análisis compartido registró **14,9 % de cobertura general**, generado con **57 pruebas de dominio aprobadas**. Ese porcentaje incluye también código de otras capas. Enviar el informe a SonarQube no implica aprobar su Quality Gate: los avisos se revisan en el panel y se contrastan con el código.

## Preparación inicial

Se necesita .NET SDK 8 y Docker Desktop. Todos los comandos siguientes se ejecutan desde la raíz, en PowerShell.

Si aún no tienes `.env`, créalo y completa sus contraseñas:

```powershell
Copy-Item .env.example .env
dotnet restore SolarUY.sln
docker compose up -d
```

Guarda la conexión y los secretos de los clientes de Keycloak, reemplazando los ejemplos. El puerto habitual del Compose es 3307; comprueba el tuyo con `docker compose port mariadb 3306`. La contraseña, usuario y base deben coincidir con `.env`, que no se carga automáticamente en .NET.

```powershell
dotnet user-secrets set "ConnectionStrings:SolarUY" "Server=127.0.0.1;Port=3307;Database=solaruy;User=solaruy;Password=TU_PASSWORD_MARIADB;" --project SolarUY.Presentation
dotnet user-secrets set "Authentication:OpenIdConnect:ClientSecret" "SECRETO_DE_SOLARUY_WEB" --project SolarUY.Presentation
dotnet user-secrets set "Keycloak:Directory:ClientSecret" "SECRETO_DE_SOLARUY_TECNICO_DIRECTORY" --project SolarUY.Presentation
```

Los secretos se copian de **Keycloak → Clients → cliente → Credentials**. La configuración de login y mantenimiento está en [keycloak/README.md](keycloak/README.md) y [keycloak/TECNICOS.md](keycloak/TECNICOS.md). Los usuarios de prueba son `admin.test`, `tecnico.test` y `consulta.test`.

Instala la herramienta de EF si no la tienes y aplica las migraciones. El último comando también se usa cuando cambia la estructura de la base:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.31
dotnet ef database update --project SolarUY.Infrastructure/SolarUY.Infrastructure.csproj --startup-project SolarUY.Presentation/SolarUY.Presentation.csproj -- --environment Development
```

## Iniciar y detener

Inicia Docker y deja el simulador funcionando en una terminal:

```powershell
docker compose up -d
dotnet run --project SolarUY.InversorSimulador/SolarUY.InversorSimulador.csproj
```

En otra terminal, inicia la web:

```powershell
dotnet run --project SolarUY.Presentation/SolarUY.Presentation.csproj --launch-profile http
```

Web: http://localhost:5219 · Keycloak: http://localhost:8080 · phpMyAdmin: http://localhost:8081.

Como alternativa al último comando, elige un modo del script. **Solo inicia la web; Docker y el simulador se ejecutan aparte.**

| Comando | Resultado |
| --- | --- |
| `.\Iniciar-Demo.ps1` | Conserva los datos y guarda nuevas lecturas. |
| `.\Iniciar-Demo.ps1 -SinGuardarHistorial` | Conserva los datos sin guardar nuevas lecturas. |
| `.\Iniciar-Demo.ps1 -ReiniciarDatos` | Restaura los datos del SQL y vuelve a guardar lecturas. |
| `.\Iniciar-Demo.ps1 -ReiniciarDatos -SinGuardarHistorial` | Restaura los datos del SQL y mantiene el historial vacío. |

**ReiniciarDatos reemplaza los datos actuales de SolarUY** importando una sola vez [solaruy_pruebas.sql](solaruy_pruebas.sql). Detén la web antes de usarlo. El SQL carga 2 parques, 4 nodos, 4 inversores, 12 paneles y 3 tareas, sin lecturas; también se importa desde phpMyAdmin. En otro Keycloak, ajusta el ID del técnico con `-TecnicoId 'ID-REAL-DEL-TECNICO'`.

El restablecimiento usa la base del servicio MariaDB de Compose; la conexión .NET debe apuntar a ella. SinGuardarHistorial afecta solo a esa ejecución y mantiene el monitoreo en vivo.

Para detener la web y el simulador, pulsa **Ctrl + C** en sus terminales. Detén Docker conservando los datos con `docker compose stop`. Para volver, repite los comandos de inicio.

## Comprobaciones

```powershell
dotnet build SolarUY.sln
dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj
dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj
dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj --collect:"XPlat Code Coverage"
dotnet test SolarUY.sln
```

La cobertura se genera en `SolarUY.Domain.Tests/TestResults`; no se envía automáticamente a SonarQube. Algunas pruebas requieren una base aislada: ver [SolarUY.Integration.Tests/README.md](SolarUY.Integration.Tests/README.md).
