# Pruebas de integración del simulador (REQ005)

Ejecutar desde la raíz con .NET 8:

```powershell
dotnet test SolarUY.sln
```

Para ejecutar únicamente esta suite:

```powershell
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj
```

Los 19 casos utilizan xUnit y las versiones de Test SDK/runner ya usadas por Domain.Tests. Resuelven `IInversorService` con `InversorService`, `IInversorClient` y el registro real `AddSolarInfrastructure`. Cada prueba tiene su contenedor; las comprobaciones de singleton usan varios ámbitos del mismo contenedor.

Solo se sustituyen las fronteras de reloj e identidad por valores controlados. La configuración es exclusivamente en memoria, sin conexión, `.env`, user-secrets, MariaDB ni Keycloak. Una máquina sin servicios Docker puede ejecutar la suite. Estas pruebas verifican integración de componentes de simulación y autorización; no son recorridos OIDC ni pruebas de persistencia.

Cobertura: topología predeterminada/configurable, consulta y potencia total, noche/amanecer/mediodía/tarde/anochecer, zona horaria, fallas, activar/desactivar e idempotencia, aislamiento por panel/nodo, volcado a red, estado compartido entre ámbitos y reinicio, permisos de los tres roles, identidad inválida, identificadores desconocidos y cancelación.
