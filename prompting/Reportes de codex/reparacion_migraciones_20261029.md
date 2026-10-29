# Reparación de migraciones y preservación de SolarUY

Fecha: 29/10/2026, America/Montevideo. Trabajo ejecutado sobre MariaDB 11.8.9 en `solaruy-mariadb-1`, publicado en `127.0.0.1:3307`, base `solaruy`. No se imprimieron cadenas de conexión completas ni contraseñas.

## 1. Diagnóstico y causas

- **Migración bloqueada:** `AddRecoleccionPeriodica` exigía probar que el nodo actual de cada panel era también el de todas sus lecturas. La base, la exportación, los scripts y el historial Git disponibles no contienen una auditoría que permita demostrarlo. Un setter privado en el modelo y la coincidencia entre dos respaldos no demuestran que nunca hubiera cambios. No se certificó esa equivalencia.
- **`Unknown column l.NodoId`:** el modelo nuevo consultaba una columna todavía inexistente porque la migración anterior se detenía antes del DDL.
- **`CursoresMonitoreo doesn't exist`:** la migración de distribución de monitoreo dependía de terminar la anterior; sus tres tablas aún no existían.
- **Conexión sobrescrita:** el user-secret compartido `solaruy-presentation-local` apuntaba a `localhost:3306`; en Development prevalecía sobre el `appsettings.json` local que ya apuntaba a `127.0.0.1:3307`. Se actualizó únicamente ese secreto con la conexión local configurada. La fábrica de EF ahora resuelve el directorio de Presentation y su JSON de entorno de manera estable, aplica secretos solo en Development y conserva la prioridad de las variables de entorno. EF muestra destino y proveedor; la aplicación registra solo servidor, puerto y base.
- **TCP 10061:** no había listener en `127.0.0.1:5055`. REQ013 establece expresamente un proceso independiente. `SolarUY.InversorSimulador/Program.cs` registra el motor local e invoca `InversorTcpServer.EjecutarAsync`; Presentation registra el cliente TCP. Se inició el ejecutable previsto, sin alojar un segundo servidor dentro de cada instancia web.
- La advertencia de redirección HTTPS del perfil HTTP es independiente de esos errores y sigue siendo una configuración de perfil; no bloqueó el arranque ni la respuesta de autenticación.

## 2. Estrategia aplicada

Se conservan en `LecturasPanel` todas las filas originales. `NodoId = NULL` expresa procedencia anterior no demostrada. No se utiliza `Paneles.NodoId` para rellenarlas, no se crean nodos ficticios y la antigua variable de sesión ya no autoriza rellenos masivos.

El constructor público de `LecturaPanel` sigue exigiendo un panel con nodo y captura su asociación al medir. El repositorio de recolección exige además navegación y nodo en cada lectura nueva. Se mantienen FK restrictivas, precisión UTC y unicidad `(PanelId, FechaHora)`.

El histórico por panel conserva el acceso a las lecturas legadas. Los agregados por nodo/parque utilizan el nodo guardado en la lectura; incluyen paneles trasladados y excluyen las muestras sin procedencia. Se calcula el fin del tramo usando la siguiente muestra antes de filtrar su nodo, para que una muestra desconocida o de otro nodo corte la integración anterior. El monitoreo identifica el nodo histórico desconocido y no suma su potencia como vigente.

Histórico, reportes y CSV informan la existencia de lecturas sin nodo. Para nodo/parque el aviso abarca toda la base en el período, incluyendo el margen de cinco minutos usado por la integración: no afirma que esas lecturas pertenecieran al objetivo seleccionado. No se inventan totales ni se representa ausencia de cobertura como cero medido.

La nulabilidad es una excepción de compatibilidad del legado, no una autorización para crear nuevas mediciones sin nodo. Una asignación histórica futura requerirá evidencia por panel e intervalo, documentación del responsable y una operación específica y respaldada. Esa asignación no se ejecutó ni es necesaria para completar las migraciones.

## 3. Respaldo, contraste e integridad

Antes de modificar `solaruy`, se comprobó que había cuatro migraciones registradas y que no existía DDL parcial de `AddRecoleccionPeriodica`: no había columna NodoId, índices nuevos ni tablas de distribución.

Se generó un volcado completo con `mariadb-dump --single-transaction --routines --events --triggers --hex-blob --skip-add-drop-table`. Se restauró en `solaruy_repair_20261029` y se probó allí toda la cadena de migraciones antes de aplicarla a la base habitual.

Respaldos locales, excluidos de Git por `artifacts/`:

| Archivo | SHA-256 |
| --- | --- |
| `artifacts/database-repair/before.sql` (206.251 bytes) | `DFC91FACC81E335E048165B3154C6A8CCA389C1A91E58547272149BEF87F06FF` |
| `artifacts/database-repair/after.sql` | `2C84E3463E92F9C5AFDCEA0738B77B58CCD74C0FA07D808B09CB4DE88B4C8C16` |

Las copias antiguas `backup-solaruy*.sql` del repositorio tenían 12.265 bytes y no contenían estas lecturas, por lo que no se usaron como respaldo de recuperación.

También se encontró `C:\Users\Juanpi\Downloads\solaruy (1).sql`. Se restauró aparte en `solaruy_export_check_20261029`: contiene las mismas 1.578 lecturas, con cero diferencias en Id, PanelId, FechaHora, PotenciaGeneradaW y EstadoSalud. No contiene auditoría histórica de asignaciones.

Resultados comprobados antes, después de migrar y nuevamente después de recuperar Docker:

| Comprobación | Antes | Después |
| --- | ---: | ---: |
| Lecturas originales | 1.578 | 1.578 |
| Paneles con lecturas | 6 | 6 |
| Lecturas por panel | 263 | 263 |
| Diferencias en los cinco campos originales | 0 | 0 |
| Filas adicionales en la base habitual durante la verificación | 0 | 0 |
| Duplicados panel/instante | 0 | 0 |
| Lecturas sin panel existente | 0 | 0 |
| Paneles sin nodo existente | 0 | 0 |
| Nodos sin parque, inversores sin nodo, tareas sin panel | 0 | 0 |
| Nodo histórico legado desconocido | No había columna | 1.578 NULL |

El período conservado es `2026-10-22 14:30:59.665405` a `2026-10-22 19:03:19.022700` UTC. Se contrastaron ambos conjuntos de identificadores y todos sus valores originales; no se comparó únicamente el número de filas.

Las tablas restantes mantienen 2 parques, 3 nodos, 6 paneles, 0 inversores y 6 tareas. `CHECKSUM TABLE ... EXTENDED` coincidió contra la copia restaurada y la exportación:

| Tabla | Checksum idéntico |
| --- | ---: |
| ParquesSolares | 1809500179 |
| Nodos | 4273510465 |
| Paneles | 2027136801 |
| Inversores | 0 |
| TareasMantenimiento | 2373066468 |

Se comprobó la nueva FK `LecturasPanel.NodoId → Nodos.Id`, la FK al panel, los índices y ausencia de nodos históricos huérfanos. No se desactivaron FK para ocultar inconsistencias. El arranque web creó su cursor operativo de monitoreo; no alteró lecturas ni inventario.

## 4. Migraciones reales

| Migración | Estado inicial | Estado comprobado final |
| --- | --- | --- |
| `20261008040649_AddParqueSolar` | Aplicada | Aplicada, sin cambios |
| `20261015023351_AddMantenimientoPaneles` | Aplicada | Aplicada, sin cambios |
| `20261022130747_AddLecturaPanel` | Aplicada | Aplicada, sin cambios |
| `20261028235200_AddInversorNodo` | Aplicada | Aplicada, sin cambios |
| `20261029023745_AddRecoleccionPeriodica` | Pendiente, sin DDL parcial | Aplicada con conservación del legado |
| `20261029120000_AddDistribucionMonitoreo` | Pendiente | Aplicada |
| `20261029130000_PreserveUnknownHistoricalNodes` | Nueva | Aplicada |
| `20261029140000_AddHistoricalNodeCoverage` | Nueva | Aplicada |

Solo se corrigió el código de una migración que se comprobó pendiente; no se reescribieron las cuatro aplicadas inicialmente. La migración nueva de compatibilidad admite instalaciones que ya tuvieran la versión original con nodo obligatorio y conserva sus nodos guardados. Su `Down` se detiene si hay lecturas sin nodo: no rellena Guid.Empty ni pierde información.

La migración de cobertura agrega `(PanelId, FechaHora, NodoId, PotenciaGeneradaW)` y conserva el índice de cobertura anterior. El snapshot y los modelos nuevos se generaron con EF Core. `migrations has-pending-model-changes` informó que no hay diferencias y `migrations list` no mostró pendientes.

Las operaciones DDL de MariaDB tienen commits implícitos; no se prometió rollback atómico. La prueba en copia, el respaldo y la inspección del esquema precedieron a la operación real. Si una ejecución futura se interrumpe, inspeccionar columnas, índices, FK e historial antes de continuar; no insertar filas de historial manualmente. Véase [documentación de MariaDB sobre commits implícitos](https://mariadb.com/docs/server/reference/sql-statements/transactions/sql-statements-that-cause-an-implicit-commit) y [aplicación de migraciones de EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

## 5. Archivos modificados y propósito

- `SolarUY.Domain/Entities/LecturaPanel.cs`, `SolarUY.Domain/Monitoreo/GeneracionActualService.cs` y sus pruebas de Domain: representar legado desconocido y excluirlo de la potencia vigente, manteniendo constructor y lecturas nuevas coherentes.
- `SolarUY.Application/HistoricoGeneracion/ContratosHistorico.cs`, `SolarUY.Application/Reportes/ReporteCsv.cs`: transportar y exportar el aviso de procedencia desconocida.
- `SolarUY.Infrastructure/DependencyInjection.cs`, `Persistence/ApplicationDbContextFactory.cs`: destino efectivo verificable sin secretos y configuración de EF consistente entre directorios y entornos.
- `Persistence/LecturaPanelConfiguration.cs`, `RecoleccionLecturasRepository.cs`: relación histórica opcional para el legado, índices y exigencia de nodo al recolectar.
- `Persistence/HistoricoGeneracionRepository.cs`: filtros históricos, cortes correctos de tramos y consultas agregadas en servidor. La unicidad del esquema permite eliminar AVG/GROUP BY redundantes. El plan comienza por los paneles seleccionados y utiliza cobertura, evitando barridos globales repetidos.
- `Migrations/20261029023745_AddRecoleccionPeriodica.cs`, las dos migraciones nuevas, sus `.Designer.cs` y `ApplicationDbContextModelSnapshot.cs`: esquema incremental compatible, sin reconstruir tablas ni eliminar datos.
- `SolarUY.Presentation/Components/Shared/ResultadoHistorico.razor`, `Components/Pages/Monitoreo.razor`: mensajes de procedencia. `SolarUY.Presentation.csproj`: excluir siempre bin/obj de los elementos copiados, también con salidas de compilación alternativas.
- `SolarUY.Integration.Tests/RecoleccionMigracionIntegrationTests.cs`, nuevo `ProcedenciaHistoricaIntegrationTests.cs`, `ReportesIntegrationTests.cs`: regresiones de preservación, compatibilidad, reversión, filtros, tramos y CSV.
- `RecoleccionLecturasIntegrationTests.cs`: corregir la expectativa que sumaba un panel del otro parque; ahora exige exactamente los tres paneles propios. No se relajó la aserción del resultado.
- `HistoricoDatabase.cs`, `MonitoreoHubIntegrationTests.cs`, `MonitoreoNginxIntegrationTests.cs`: resolver recursos desde SolarUY.sln, sin asumir una profundidad fija del directorio de salida.
- README principal, README de integración y documentos de REQ007/008/011: conexión, intervalo vigente, excepción de legado y evidencia de esta reparación.

El `appsettings.json` ya estaba modificado al iniciar la tarea; se conservó ese cambio sin editarlo. No se modificaron `CodeQuality.Tests`, `.agents/rules` ni `.agents/skills`. No se añadieron dependencias ni se cambiaron versiones.

## 6. Ejecución y simulador

Se inició `SolarUY.InversorSimulador` como proceso independiente. Una consulta real mediante `InversorTcpClient` en `127.0.0.1:5055` sincronizó los tres nodos y seis paneles de MariaDB; respondió encendido y devolvió potencia simulada para los nodos Norte, Sur y Principal. Las pruebas TCP verificaron también desconexión, JSON inválido y órdenes de estado.

La web arrancó desde Presentation con `dotnet run --no-build`, sin forzar la conexión por entorno, usando el user-secret corregido. Escuchó en `http://localhost:5219`, respondió HTTP 302 de autenticación y el worker de distribución utilizó CursoresMonitoreo correctamente. Se consultaron el histórico real y el repositorio de monitoreo sin los errores A/B. En esa comprobación se deshabilitó la recolección solo por entorno para mantener exactamente 1.578 filas, y luego se detuvo la web. La configuración persistente mantiene recolección habilitada a 60 segundos; el arranque normal añadirá mediciones nuevas con nodo conocido.

## 7. Pruebas y límites

Se ejecutaron build, tests de dominio/aplicación/arquitectura, migraciones desde cero y esquemas anteriores, preservación del legado, compatibilidad con nodo guardado, reversión segura, FK e índices, histórico, reportes/CSV, monitoreo, recolección periódica, concurrencia entre procesos y TCP. Evidencia TRX en `artifacts/database-repair/`.

La corrida funcional final aprobó 152 casos de integración, 57 de Domain, 16 de Application y 9 de arquitectura. Sumada la prueba de volumen separada, son **235 pruebas distintas aprobadas, cero fallidas y tres omitidas**. Los tres casos omitidos requieren navegador interactivo/Playwright o Nginx; no se presentan como aprobados. La imagen fijada de Nginx y las dependencias de Playwright no estaban instaladas. Los TRX finales están en `completed-functional/` y `completed-performance/`; las carpetas anteriores conservan corridas intermedias que detectaron problemas y no representan el resultado final.

La compilación completa terminó con cero advertencias y cero errores. Se ejecutó además el comando obligatorio de `CodeQuality.Tests`; sus nueve casos pasaron sin editar sus archivos.

La prueba original de un millón de lecturas aprobó los diez escenarios, con cuatro consultas concurrentes en cada uno y el plan SQL real inspeccionado. Se mantuvieron los límites de cinco segundos por consulta y menos de 256 MiB adicionales. No se modificó esa prueba ni sus aserciones.

| Escenario | Máximo de cuatro ejecuciones (s) |
| --- | ---: |
| Parque diario | 3,057 |
| Parque mensual | 3,834 |
| Parque anual | 3,363 |
| Nodo diario | 1,802 |
| Nodo mensual | 3,162 |
| Nodo anual | 2,826 |
| Panel diario | 0,016 |
| Panel mensual | 0,039 |
| Panel anual | 0,023 |
| Comparación parque/mes | 4,437 |

El máximo de memoria adicional fue 11,31 MiB. Evidencia: `artifacts/database-repair/completed-performance/`, TRX del 29/10/2026 a las 03:30. `DefaultCommandTimeout=120` se usó solo en la conexión aislada para la preparación/limpieza masiva; no modifica esos límites medidos ni la conexión de la aplicación.

Durante las verificaciones aparecieron bloqueos de DLL de procesos de pruebas y copias recursivas de salidas anteriores al cambiar BaseOutputPath. Se corrigió la exclusión de bin/obj y se usaron salidas aisladas. Una carga de volumen agotó el espacio y bloqueó Docker; se retiraron exclusivamente binarios recompilables y bases de carga de esta sesión, se recuperó Docker sin borrar volúmenes y se repitió la comparación completa de la base original. MariaDB, Keycloak, phpMyAdmin y los demás contenedores que estaban activos volvieron a arrancar. El respaldo original y el posterior permanecen íntegros.

## 8. Riesgos y decisiones pendientes

- No existe evidencia del nodo original de las 1.578 lecturas; quedan expresamente sin atribuir. Cualquier asignación posterior requiere esa evidencia, no una confirmación genérica ni una variable de sesión.
- Las verificaciones manuales de navegador y Nginx no se sustituyen por las pruebas de servidor. Keycloak no se reconfiguró y no se cambiaron usuarios ni permisos reales.
- Las cargas de un millón de filas requieren espacio adicional; revisar el espacio de Windows y Docker antes de repetirlas. Las bases temporales de pruebas no son respaldos de producción.
- La web no ejecuta migraciones automáticamente. Detener recolectores y operar con un único proceso de migración en actualizaciones futuras.

## 9. Comandos de operación

Desde la raíz del repositorio, las migraciones ya están aplicadas. Para comprobarlas:

```powershell
dotnet ef migrations list --project SolarUY.Infrastructure --startup-project SolarUY.Presentation
dotnet ef migrations has-pending-model-changes --project SolarUY.Infrastructure --startup-project SolarUY.Presentation
```

El simulador debe estar activo antes de arrancar la web. Si el proceso de esta verificación sigue escuchando en 5055, no iniciar otro en ese puerto. Cuando se haya detenido, iniciarlo en su propia terminal:

```powershell
dotnet run --project SolarUY.InversorSimulador/SolarUY.InversorSimulador.csproj
```

En otra terminal:

```powershell
dotnet run --project SolarUY.Presentation/SolarUY.Presentation.csproj --launch-profile http
```

También funciona el flujo habitual `dotnet run` desde `SolarUY.Presentation`. El secreto local de Development ya apunta a 3307; no hace falta forzar una variable para corregirlo. Una variable `ConnectionStrings__SolarUY` que exista en una terminal tendrá prioridad, como corresponde a la configuración de .NET.

Para repetir tests, usar una conexión externa exclusiva cuyo nombre comience por `solaruy_req007_test`; nunca `solaruy`. Ver el README de integración. No ejecutar comandos de borrado, recreación ni restauración sobre la base habitual para ponerla al día.

Comandos finales de compilación y tests, con `SOLARUY_HISTORICO_TEST_CONNECTION` configurada de forma privada para esa base aislada:

```powershell
dotnet build SolarUY.sln --no-restore -m:1 -p:BaseOutputPath=bin/DatabaseRepairCompleted/
dotnet test SolarUY.sln --no-build --no-restore -p:BaseOutputPath=bin/DatabaseRepairCompleted/ --filter 'FullyQualifiedName!~Volumen_UnMillon' --logger trx --results-directory artifacts/database-repair/completed-functional
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --no-build --no-restore -p:BaseOutputPath=bin/DatabaseRepairCompleted/ --filter 'FullyQualifiedName~Volumen_UnMillon' --logger 'console;verbosity=detailed' --logger trx --results-directory artifacts/database-repair/completed-performance
```

Durante esta reparación se ejecutó volumen antes de la última corrida funcional y se retiró exclusivamente su base temporal entre ambas, evitando arrastrar la limpieza de un millón de filas al resto de las verificaciones. En una repetición ordinaria debe reservarse espacio suficiente y tiempo para esa preparación.
