# Implementación: Histórico de generación (REQ007)

Fecha: 22/10/2026. El histórico está disponible en `/historico-generacion`. La ampliación R1 incorpora recolección automática del inventario y está activa en la aplicación local cada minuto. La entrega inicial completó las 40 tareas originales; el estado actual y la verificación de volumen reabierta se registran en `tasks.md`.

## Comportamiento implementado

- Consulta diaria, mensual, anual y por rango de un parque, nodo o panel, incluyendo comparación de períodos del mismo objetivo.
- Energía en kWh mediante integración de potencia constante hasta la siguiente muestra, con vigencia máxima de cinco minutos, promedio de duplicados y recorte exacto a `[inicio, fin)`. Se procesan también las muestras auxiliares de los cinco minutos anteriores al inicio.
- Calendario de Uruguay: serie horaria para día, diaria para mes/rango y mensual para año. Se respetan días históricos de 23/25 horas; las entradas locales ambiguas o inexistentes se rechazan.
- Totales y extremos sobre la energía observada; se muestran todos los empates. Los intervalos sin cobertura conservan energía nula, diferenciada de cero medido, y se indica cobertura parcial/completa/sin datos.
- Lectura global para Administrador, Tecnico y Consulta, autorizada por Application en consulta, comparación y opciones, incluso cuando se invoca directamente el servicio.
- Blazor/Radzen presenta selección, fechas, resultados, comparación, extremos y estados de validación/carga/error. No calcula energía ni consulta infraestructura desde los componentes.

## Archivos incorporados o modificados

| Capa/etapa | Archivos |
| --- | --- |
| Domain | `SolarUY.Domain/Entities/LecturaPanel.cs`, `SolarUY.Domain/Enums/EstadoSaludLectura.cs`. |
| Domain.Tests | `SolarUY.Domain.Tests/LecturaPanelTests.cs`: cuatro casos para constructor, identidad, asociaciones, UTC, estado, potencia y precisión. |
| Application | `SolarUY.Application/HistoricoGeneracion/ContratosHistorico.cs`, `HistoricoGeneracionService.cs`: contratos, validación, calendario, autorización y orquestación. |
| Infrastructure | `SolarUY.Infrastructure/Persistence/LecturaPanelConfiguration.cs`, `HistoricoGeneracionRepository.cs`, `ApplicationDbContext.cs` y `SolarUY.Infrastructure/DependencyInjection.cs`. |
| Migración | `SolarUY.Infrastructure/Migrations/20261022130747_AddLecturaPanel.cs`, su `.Designer.cs` y `ApplicationDbContextModelSnapshot.cs`. No se alteraron las migraciones históricas. |
| Presentation | `SolarUY.Presentation/Components/Pages/HistoricoGeneracion.razor`, `Components/Shared/ResultadoHistorico.razor`, `Components/Layout/NavMenu.razor` y `Program.cs`. |
| Integración | `SolarUY.Integration.Tests/HistoricoDatabase.cs`, `HistoricoGeneracionIntegrationTests.cs` y `README.md`: configuración aislada, preparación de datos, 26 casos de req007 y forma de ejecución. |
| Documentación | `specs.md`, `tasks.md` y este `implementations.md`. |

En la entrega inicial no se modificaron requirements, reglas, habilidades, pruebas arquitectónicas, dependencias ni versiones autorizadas. La ampliación R1 actualiza el alcance de requirements y agrega una referencia del proyecto de integración a Presentation para probar el worker real; conserva los paquetes y versiones existentes. Se preservan los cambios del usuario ajenos a req007.

## Persistencia y consulta

`LecturasPanel` almacena la relación con el panel, fecha UTC, potencia decimal(18,6) no negativa y salud Sano/Fallado/Desconocido. La salud no sustituye la potencia medida. La FK restrictiva es compatible con la baja explícita existente de req006; esa operación se probó en una base aislada.

La consulta usa parámetros, contexto por operación, selección de paneles materializada una vez, promedio de duplicados, `LEAD`, límites enviados mediante `JSON_TABLE`, integración, cobertura y funciones de ventana para totales/extremos. Solo devuelve filas agregadas a la aplicación. Las consultas no escriben ni modifican lecturas.

La prueba inicial de volumen encontró un tiempo superior al umbral. Se corrigió mediante índice de cobertura `(PanelId, FechaHora, PotenciaGeneradaW)`, unión por día UTC, eliminación de ordenación redundante y selección materializada de paneles. `SET STATEMENT` permite hasta 256 MiB por tabla temporal de esta consulta sin modificar la configuración global de MariaDB; esta memoria del motor es distinta de la memoria adicional medida en la aplicación.

Referencias de sintaxis consultadas: [JSON_TABLE](https://mariadb.com/docs/server/reference/sql-functions/special-functions/json-functions/json_table) y [LEAD](https://mariadb.com/docs/server/reference/sql-functions/special-functions/window-functions/lead), documentación oficial de MariaDB.

## Verificaciones realizadas

### Solución completa

```powershell
dotnet test SolarUY.sln --no-restore -m:1 --disable-build-servers -p:BaseOutputPath=bin/req007/ --logger trx --results-directory artifacts/req007/final-tests
```

Se configuró `SOLARUY_HISTORICO_TEST_CONNECTION` externamente contra MariaDB aislada. Se compiló en una salida separada porque la instancia habitual del usuario mantenía bloqueados sus binarios.

| Suite | Correctos | Fallos | Omitidos |
| --- | ---: | ---: | ---: |
| CodeQuality.Tests | 9 | 0 | 0 |
| SolarUY.Domain.Tests | 46 | 0 | 0 |
| SolarUY.Application.Tests, existentes | 8 | 0 | 0 |
| SolarUY.Integration.Tests, existentes y nuevos | 45 | 0 | 0 |
| **Total** | **108** | **0** | **0** |

Los nuevos casos comprueban las nueve combinaciones de período/objetivo, aislamiento, datos conocidos, deduplicación, muestras de borde, huecos/cero, extremos y empates, comparación, cambios de mes/año, bisiesto, cambios históricos de horario, permisos, validación, cancelación, UTC, conservación de lecturas y baja de un parque conservando el otro.

### Migraciones

- Aplicación satisfactoria de las tres migraciones desde cero en `solaruy_req007_test_migration`, además de su uso en `solaruy_req007_tests`.
- `dotnet ef migrations has-pending-model-changes --project SolarUY.Infrastructure --no-build`: sin cambios de modelo pendientes.
- Se verificó UTF-8 sin BOM y ausencia de errores de espacios en los archivos de entrega.

### Volumen

Entorno medido: Windows 10.0.26200, .NET 8.0.31, 12 CPU lógicas, Docker 28.4 y MariaDB 11.8.9. El contenedor conserva el buffer pool predeterminado de 128 MiB. Datos: un millón de lecturas, 100 paneles, 10000 muestras por panel cada cinco minutos desde 01/01/2026 en Uruguay. Se ejecutaron cuatro consultas concurrentes por escenario; la preparación de datos se excluyó de la latencia.

Resultados de la regresión final, mostrando el máximo de las cuatro consultas:

| Escenario | Latencia máxima (s) | Memoria adicional máxima de la aplicación (MiB) |
| --- | ---: | ---: |
| Parque / día | 1,671 | 0,62 |
| Parque / mes | 4,537 | 3,05 |
| Parque / año | 4,491 | 0,95 |
| Nodo / día | 0,537 | 1,10 |
| Nodo / mes | 3,960 | 0,03 |
| Nodo / año | 3,890 | 0,89 |
| Panel / día | 0,011 | 0,00 |
| Panel / mes | 0,034 | 0,00 |
| Panel / año | 0,044 | 0,00 |
| Comparación parque / mes | 4,393 | 1,18 |

Todos los escenarios cumplieron menos de cinco segundos por consulta y menos de 256 MiB adicionales en la aplicación. El plan del comando real confirma uso del índice compuesto, filtrado por panel/tiempo y ventanas/agregación en el servidor. Estas cifras corresponden al entorno y distribución indicados.

### Navegador

Chrome sin ventana contra una instancia temporal HTTPS 7272, MariaDB aislada en 3307 y Keycloak aislado en 8082, con usuarios de prueba. **31 comprobaciones correctas**, sin excepciones JavaScript ni errores de consulta/circuito.

Para Administrador, Tecnico y Consulta se comprobó menú/página, selección de parque/nodo/panel, consulta diaria/mensual/anual, valores conocidos, extremos, zona horaria, huecos y comparación. Se verificaron rango invertido, ausencia de lecturas y denegación de acceso directo/menú para un rol sin permiso. Se inspeccionaron capturas de escritorio y de ancho 390 px. No se usaron credenciales reales ni se alteró Keycloak habitual.

Evidencia local, ignorada por Git: `artifacts/req007/final-tests/*.trx`, `browser-results.json`, `browser.mjs`, `captures/` y registros del host. Las pruebas permanentes de backend quedan en los proyectos de la solución; los artefactos de navegador son evidencia local.

Al finalizar se detuvieron el host y navegador temporales y se retiraron los tres contenedores de pruebas con sus volúmenes temporales. Permanecen activos los servicios habituales de MariaDB, Keycloak y phpMyAdmin. La evidencia local se conserva.

## Corrección en el entorno habitual — 22/10/2026

Tras el reporte de error de la pantalla, se reprodujo la causa en la base local `solaruy` de localhost:3306: error SQL 1146, `Table 'solaruy.LecturasPanel' doesn't exist`. La migración `20261022130747_AddLecturaPanel` seguía pendiente en esta base.

Se aplicó únicamente esa migración mediante `dotnet ef database update 20261022130747_AddLecturaPanel --project SolarUY.Infrastructure --no-build`. No quedan migraciones pendientes. Se conservaron los dos parques, tres nodos, seis paneles y seis tareas: la huella SHA-256 de todos sus atributos y registros fue idéntica antes y después.

Se verificaron con el servicio y repositorio reales las consultas diarias, mensuales, anuales y por rango de Parque Solar Maldonado, una consulta de nodo, una de panel y la comparación de períodos. Todas finalizaron correctamente y devolvieron `SinDatos`, que corresponde a la ausencia actual de lecturas. No se fabricaron lecturas ni se modificó la fuente productiva de datos.

Se ejecutaron los nueve controles de `CodeQuality.Tests`, todos correctos. No se repitieron la carga masiva ni el recorrido de navegador: esta corrección aplica el esquema ya verificado, sin cambios en el código del producto. Evidencia local sin credenciales: `artifacts/req007-localfix/before.json`, `after.json` y el verificador de consultas de esa carpeta. La pantalla ya compilada puede volver a consultar sin reiniciar la aplicación.

## Aplicación en otras instalaciones

En la entrega inicial, la migración se había aplicado únicamente en bases aisladas; la corrección anterior completa su aplicación en la base habitual. Para habilitar la página en otra instalación, configurar externamente la conexión correspondiente y ejecutar:

```powershell
dotnet ef database update --project SolarUY.Infrastructure
```

Recompilar y reiniciar la instancia habitual para utilizar el código nuevo; luego acceder a `/historico-generacion`. La consulta consume lecturas persistidas del inventario. La ampliación R1 agrega la recolección automática mediante el simulador para paneles registrados, conservando independiente la topología de la pantalla de simulación de req005.

La cobertura utiliza los paneles actuales del objetivo. No se reconstruyen cambios históricos de pertenencia/instalación que el modelo no registra, no se imputan huecos y no se normalizan períodos de diferente duración. Estas son las reglas acordadas, no verificaciones pendientes.

## Ampliación R1: recolección periódica del inventario

La aplicación recoge al arrancar y luego cada minuto mediciones simuladas de los paneles registrados. Usa sus Guid, número de serie, potencia nominal y estado; la curva solar se evalúa entre 06:00 y 18:00 de Uruguay. Se guardan fecha UTC, potencia en W y salud, incluyendo cero medido durante la noche, inactividad o falla. No se crean registros anteriores a la activación.

El servicio interno de Application obtiene una sesión exclusiva, consulta paneles instalados sin lectura en el minuto actual, recibe y valida el lote de mediciones y construye las entidades existentes. Solo después de validar el lote completo se guarda de forma atómica. Se vuelve a consultar el inventario en cada ciclo, incluyendo nuevos paneles sin cambiar los registros anteriores.

La sesión conserva una conexión física no agrupada a MariaDB con un `GET_LOCK` cuyo nombre depende de la base. El bloqueo abarca la lectura de la fuente y el guardado; otras instancias omiten el ciclo. `RELEASE_LOCK` y el cierre de conexión liberan la sesión también ante error o cancelación. El filtro por panel/minuto evita duplicar datos ante reinicios o instancias que intenten recoger secuencialmente. No se cambia el esquema ni se impone una restricción que impida importar duplicados históricos.

`RecoleccionLecturasWorker`, alojado por el host, crea un ámbito DI por ciclo, recoge sin solapar ciclos locales y reintenta errores en la siguiente ejecución. No depende de un usuario conectado ni expone un endpoint de escritura al chatbot. Los fallos del recolector se registran y no detienen la web.

### Archivos de la ampliación

- `SolarUY.Application/RecoleccionLecturas/ContratosRecoleccion.cs` y `RecoleccionLecturasService.cs`.
- `SolarUY.Application/Abstractions/IInversorClient.cs`: operación de medición del inventario.
- `SolarUY.Infrastructure/Persistence/RecoleccionLecturasRepository.cs`, `Inversores/InversorSimulador.cs` y `DependencyInjection.cs`.
- `SolarUY.Presentation/BackgroundServices/RecoleccionLecturasWorker.cs`, `Program.cs` y `appsettings.json`.
- `SolarUY.Integration.Tests/RecoleccionLecturasIntegrationTests.cs`, referencia a Presentation en su proyecto, colección compartida en `HistoricoGeneracionIntegrationTests.cs` y `README.md`.
- Requisitos, especificación, tareas y este registro de implementación. Domain, las migraciones, los controles arquitectónicos y la consulta del histórico conservan su comportamiento anterior.

### Configuración

`RecoleccionLecturas:Habilitada` vale `true` y `RecoleccionLecturas:Intervalo` vale `00:01:00`. El intervalo admite de un segundo a cinco minutos; el límite de una lectura por panel/minuto permanece al elegir frecuencias más cortas. Puede deshabilitarse con `RecoleccionLecturas__Habilitada=false` o cambiarse el período con `RecoleccionLecturas__Intervalo`. Reiniciar la aplicación después de modificar estas opciones.

El simulador conserva la operación anterior de la pantalla y ofrece una medición separada del inventario registrado. Las fallas configuradas mediante `InversorSimulador:PanelesConFalla` pueden identificar esos paneles por serie o Guid. No se leen equipos físicos: la fuente sigue siendo simulada.

### Pruebas y limitación detectada

Los **15 casos nuevos de integración pasan**: día/noche, energía consultable desde el histórico, idempotencia y reinicio, estado/falla/instalación futura, paneles nuevos, fallas configuradas por serie/Guid, exclusión entre conexiones, lote incompleto/duplicado/ajeno/inválido, cancelación, recuperación de errores, dos hosts con el worker real y deshabilitación.

Se ejecutó la solución completa con MariaDB aislada: **122 pruebas correctas y una falla de rendimiento**. El caso existente de un millón de lecturas superó el umbral de cinco segundos en parque/mes; en la repetición sin compilaciones paralelas registró un máximo de **5,487 s**. Esa comprobación queda reabierta en la tarea 6.8. Se conservan su umbral y la consulta anterior; no se declara aprobada la prueba de volumen.

La comprobación final de todos los casos restantes, incluida arquitectura, terminó con **122/122 correctos y sin omisiones**:

```powershell
dotnet test SolarUY.sln --no-restore -m:1 --disable-build-servers -p:BaseOutputPath=bin/recoleccion/ --filter FullyQualifiedName!~Volumen --logger trx --results-directory artifacts/recoleccion/final-funcionales
```

Desglose: 9 controles arquitectónicos, 46 pruebas de Domain, 8 existentes de Application y 59 de integración (incluidas las 15 nuevas). Las suites usan `SOLARUY_HISTORICO_TEST_CONNECTION` configurada externamente con una base exclusiva de prueba. Se serializan ambas clases que preparan datos de esa base para evitar interferencias.

### Activación y verificación local

Se reinició la aplicación local en `http://localhost:5219` con la compilación verificada, configuración de desarrollo y conexiones habituales. La instancia de verificación fue PID 41456, con registros en `artifacts/recoleccion/app.log` y `app-error.log`. Posteriormente se detuvo esa instancia para liberar el puerto y permitir el inicio desde la terminal del usuario, como se detalla a continuación.

La base habitual pasó de **0 a 18 lecturas** durante los tres ciclos observados: seis muestras en cada ciclo, a las **11:30:59, 11:31:58 y 11:32:59 de Uruguay** del 22/10/2026. No hubo duplicados por panel/minuto. Las consultas del histórico de Maldonado y Rocha devolvieron energía con cobertura parcial, correspondiente al tiempo desde la activación. Los dos parques, tres nodos, seis paneles y seis tareas conservaron exactamente la misma huella SHA-256 de sus datos.

Evidencia local: `artifacts/recoleccion/local/before.json`, `after.json`, `periodic.json`, verificador de esa carpeta y resultados TRX en `artifacts/recoleccion/`. No se modificaron credenciales ni se cargó historia artificial. Para ver las nuevas mediciones en la pantalla, recargar y consultar el día actual; los días anteriores a la activación continúan sin lecturas.

### Corrección del arranque con puerto ocupado

El posterior `dotnet run --launch-profile http` falló porque el puerto 5219 seguía ocupado por la instancia de verificación PID 41456. Se comprobó su ruta ejecutable y se detuvo únicamente esa instancia. No se dejó otra aplicación en segundo plano ocupando ese puerto; el usuario puede ejecutar el comando desde `SolarUY.Presentation`.

El fallo de inicio también exponía una carrera: el worker comenzaba a resolver servicios antes de que Kestrel terminara de arrancar y podía usar `LoggerFactory` ya descartado durante la limpieza del host. Ahora recibe `IHostApplicationLifetime` y espera `ApplicationStarted` con cancelación antes de abrir el primer ámbito o registrar el inicio de la recolección. Si el arranque falla, la eliminación del host cancela la espera y el worker termina normalmente.

Se agregó una integración con Kestrel real y un puerto reservado por un `TcpListener`: verifica el error de enlace, la finalización del worker sin excepción y cero lecturas persistidas. Se mantienen las comprobaciones de recolección normal, recuperación de errores y detención entre hosts.

Verificación de esta corrección: **9/9 controles arquitectónicos y 16/16 casos de recolección correctos, sin omisiones**, contra una MariaDB aislada. Resultados TRX en `artifacts/recoleccion/arranque/` (arquitectura) y `artifacts/recoleccion/arranque-final/` (recolección). No se repitieron la solución completa ni el escenario de volumen; la limitación de rendimiento de 6.8 permanece pendiente.
