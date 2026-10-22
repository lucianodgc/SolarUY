# Pruebas de integración

## Histórico de generación (REQ007)

La ampliación de recolección añade 16 casos de integración: fuente del inventario registrado, día/noche, consumo por el histórico, idempotencia/reinicio, estado/fallas por serie o Guid, instalaciones futuras, nuevos paneles, exclusión entre conexiones, validación atómica del lote, cancelación, recuperación de errores, dos hosts con el worker real, deshabilitación y fallo de arranque de Kestrel por puerto ocupado sin recolectar ni fallar durante el cierre. Usan la misma conexión aislada que el histórico. Ambas clases pertenecen a una colección xUnit sin paralelismo para no interferir al preparar los datos.

```powershell
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter FullyQualifiedName~RecoleccionLecturasIntegrationTests
```

La suite del histórico usa el servicio y repositorio reales contra MariaDB 11.8. Requiere una conexión externa en `SOLARUY_HISTORICO_TEST_CONNECTION`; el nombre de la base debe comenzar con `solaruy_req007_test`. El fixture aplica migraciones y elimina/recrea datos exclusivamente en esa base aislada. Nunca apuntar esta variable a una base compartida: contiene preparación destructiva de datos de prueba.

```powershell
$env:SOLARUY_HISTORICO_TEST_CONNECTION = '<conexión a la base aislada solaruy_req007_tests>'
dotnet test SolarUY.sln
```

Sin la variable, los casos de MariaDB se informan como omitidos; las demás suites siguen ejecutándose. Para verificar completamente req007 debe configurarse la conexión y comprobar que no hay casos omitidos.

Incluye la matriz día/mes/año por panel/nodo/parque, datos conocidos, comparación, límites, deduplicación, integración de potencia, cobertura, extremos, permisos, conservación de lecturas y compatibilidad con la baja existente. El escenario de volumen crea un millón de lecturas (100 paneles, 10000 muestras cada cinco minutos), ejecuta cuatro consultas concurrentes por escenario y exige menos de cinco segundos por consulta y menos de 256 MiB adicionales en el proceso de pruebas. También inspecciona el plan SQL del comando real. El tiempo de preparación no forma parte de la latencia de consulta.

Para ejecutar solo req007 y conservar las mediciones:

```powershell
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter FullyQualifiedName~HistoricoGeneracionIntegrationTests --logger "console;verbosity=detailed" --logger trx --results-directory artifacts/req007/tests
```

Si una instancia local bloquea los binarios habituales, agregar `-p:BaseOutputPath=bin/req007/` al comando. La prueba de volumen usa recursos de MariaDB y su resultado corresponde al entorno registrado; no sustituye mediciones en un despliegue distinto.

## Simulador (REQ005)

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
