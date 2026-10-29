# Pruebas de integración

## Reportes (REQ008)

Los 28 casos de reportes reutilizan la conexión aislada del histórico y la colección sin paralelismo que prepara sus datos. Comprueban parque/nodo, períodos y coincidencia con el histórico, cobertura y huecos, mantenimiento completado por fecha de realización, permisos de los tres roles sin ampliar la gestión, CSV y conservación del resultado exportado.

La prueba de concurrencia integra el componente Blazor real, `HtmlRenderer`, los servicios y MariaDB. Retiene una respuesta ignorando su cancelación y la libera después de una consulta posterior: comprueba que la pantalla conserve el último resultado. No requiere navegador ni paquetes adicionales.

```powershell
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter 'FullyQualifiedName~Reportes'
```

Requiere `SOLARUY_HISTORICO_TEST_CONNECTION` con una base exclusiva cuyo nombre comience con `solaruy_req007_test`. Sin la variable, estos casos se omiten. El recorrido visual de barras/líneas, filtros y descargas CSV se registra en el informe de implementación de req008.

## Histórico de generación (REQ007)

La ampliación de recolección añade 16 casos de integración: fuente del inventario registrado, potencia independiente de la hora, consumo por el histórico, idempotencia/reinicio, estado/fallas por serie o Guid, instalaciones futuras, nuevos paneles, exclusión entre conexiones, validación atómica del lote, cancelación, recuperación de errores, dos hosts con el worker real, deshabilitación y fallo de arranque de Kestrel por puerto ocupado sin recolectar ni fallar durante el cierre. Usan la misma conexión aislada que el histórico. Ambas clases pertenecen a una colección xUnit sin paralelismo para no interferir al preparar los datos.

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

Los 19 casos de REQ005 utilizan xUnit y las versiones de Test SDK/runner ya usadas por Domain.Tests. Resuelven `IInversorService` y el motor simulado de Infrastructure con topología y reloj controlados. Cada prueba tiene su contenedor; las comprobaciones de singleton usan varios ámbitos del mismo contenedor.

Solo se sustituyen las fronteras de reloj e identidad por valores controlados. La configuración es exclusivamente en memoria, sin conexión, `.env`, user-secrets, MariaDB ni Keycloak. Una máquina sin servicios Docker puede ejecutar la suite. Estas pruebas verifican integración de componentes de simulación y autorización; no son recorridos OIDC ni pruebas de persistencia.

Cobertura: inventario sincronizado sin topología predeterminada, consulta y potencia nominal simulada a cualquier hora, fallas, activar/desactivar e idempotencia, aislamiento por panel/nodo, volcado a red, estado compartido entre ámbitos y reinicio, permisos de los tres roles, identidad inválida, identificadores desconocidos y cancelación.

## Simulador TCP remoto (REQ013)

Tres casos permanentes verifican el cliente TCP y el listener contra el motor simulado: lecturas aleatorias dentro del rango, listado de nodos, operaciones de panel y volcado, mediciones registradas, encendido/apagado global, desconexión y respuesta JSON inválida. El listener se inicia en loopback con un puerto libre durante cada prueba; no se necesitan Docker, MariaDB ni Keycloak.

```powershell
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter FullyQualifiedName~InversorTcpIntegrationTests
```
