# Pruebas de integración

## Monitoreo con una instancia (05/11/2026)

Se retiró la distribución de avisos mediante MariaDB. `SimplificacionMonitoreoIntegrationTests` comprueba que la migración elimina solo las tablas técnicas, conserva el inventario y las lecturas y permite seguir recolectando sin duplicados. Usa la misma base aislada de las pruebas históricas; nunca la base habitual.

Los escenarios de `MonitoreoNginxIntegrationTests` y su servidor para Playwright permanecen como referencia histórica y se informan explícitamente como omitidos. Dependían del transporte retirado; no validan la versión actual de una instancia. Las pruebas de autorización y notificación local de `MonitoreoHubIntegrationTests` siguen vigentes.

## Preservación de lecturas y migraciones (29/10/2026)

`RecoleccionMigracionIntegrationTests` verifica duplicados antes del DDL, conservación exacta de datos sin atribución, compatibilidad con instalaciones que ya guardaban nodo y bloqueo de la reversión si existen nodos desconocidos. `ProcedenciaHistoricaIntegrationTests` verifica consulta por panel, exclusión de atribuciones desconocidas, traslado de paneles y corte de tramos. Reportes comprueba también el aviso en CSV.

Usan exclusivamente `SOLARUY_HISTORICO_TEST_CONNECTION`, con prefijo `solaruy_req007_test` y permisos para crear/eliminar bases temporales. Nunca usar la base habitual. El fixture de páginas localiza `SolarUY.sln`, por lo que admite `BaseOutputPath` alternativo. El proyecto web excluye `bin/obj` para no copiar recursivamente salidas anteriores.

```powershell
dotnet build SolarUY.sln -p:BaseOutputPath=bin/DatabaseRepairChecked/
dotnet test SolarUY.sln --no-build --no-restore -p:BaseOutputPath=bin/DatabaseRepairChecked/ --logger trx --results-directory artifacts/database-repair/complete-tests
```

El tiempo de preparación de un millón de filas puede requerir un `DefaultCommandTimeout` mayor en la conexión de pruebas; eso no cambia el límite medido de cinco segundos ni el de memoria de la prueba. No ejecutar cargas de volumen con el disco casi lleno. El informe de reparación registra resultados y omisiones reales.

## Reportes (REQ008)

Los 28 casos de reportes reutilizan la conexión aislada del histórico y la colección sin paralelismo que prepara sus datos. Comprueban parque/nodo, períodos y coincidencia con el histórico, cobertura y huecos, mantenimiento completado por fecha de realización, permisos de los tres roles sin ampliar la gestión, CSV y conservación del resultado exportado.

La prueba de concurrencia integra el componente Blazor real, `HtmlRenderer`, los servicios y MariaDB. Retiene una respuesta ignorando su cancelación y la libera después de una consulta posterior: comprueba que la pantalla conserve el último resultado. No requiere navegador ni paquetes adicionales.

```powershell
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter 'FullyQualifiedName~Reportes'
```

Requiere `SOLARUY_HISTORICO_TEST_CONNECTION` con una base exclusiva cuyo nombre comience con `solaruy_req007_test`. Sin la variable, estos casos se omiten. El recorrido visual de barras/líneas, filtros y descargas CSV se registra en el informe de implementación de req008.

REQ015 agrega la regresión de tres selecciones sucesivas mientras la cancelación de la primera sigue pendiente. La variante con MariaDB usa los repositorios reales; `PaginaYServicios_ConservanUltimaSeleccionConCancelacionDemoradaSinMariaDb` también se ejecuta sin conexión e integra la página, Radzen y ambos servicios reales con repositorios controlados. Esta última no verifica SQL, gráficos en navegador ni descargas. Los resultados finales y la evidencia de Chrome/CSV están en `prompting/req8_Reportes_Con_Graficos_Del_Historico/implementations.md`.

`ReportesPantallaIntegrationTests` comprueba la respuesta HTTP real de `/reportes` y los recursos de Radzen/Blazor/CSV para los tres roles, además del rechazo a identidades anónimas y roles ajenos. El host usa únicamente la base aislada, no ejecuta recolectores y emite cookies de prueba solo en loopback; no incorpora rutas de ingreso a la aplicación de producción.

Para la inspección manual, con `SOLARUY_HISTORICO_TEST_CONNECTION` configurada, establecer `SOLARUY_REPORTES_UI_INSPECCION=1` y ejecutar únicamente `FullyQualifiedName~Pagina_ServidorTemporalParaVerificacionEnNavegador` de `ReportesPantallaIntegrationTests`. El servidor escucha en `http://127.0.0.1:5215`: `/pruebas/ingresar/Administrador`, `/pruebas/ingresar/Tecnico` y `/pruebas/ingresar/Consulta` abren los reportes. La preparación deja en el día actual de Uruguay dos parques: `Parque histórico` con 0,7 kWh, tres intervalos con datos (incluido cero), huecos, dos mantenimientos y un aviso de procedencia desconocida; `Otro parque` con 1 kWh. Finalizar mediante `/pruebas/finalizar`; el servidor vence a los 600 segundos o al límite de `SOLARUY_REPORTES_UI_SEGUNDOS` (máximo 1800). La prueba de servidor está omitida por defecto y no sustituye la evidencia de la inspección visual.

La regresión completa con MariaDB también necesita el simulador TCP para los escenarios existentes de monitoreo y recolección. Iniciarlo en otra terminal mediante `dotnet run --project SolarUY.InversorSimulador/SolarUY.InversorSimulador.csproj`, en `127.0.0.1:5055` por defecto, antes de `dotnet test SolarUY.sln`. La última ejecución de REQ015 completó 241 pruebas correctas y cuatro omisiones documentadas (dos servidores manuales y dos escenarios Nginx retirados), sin fallos.

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
