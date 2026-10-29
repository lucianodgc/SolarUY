# Implementación: Recolección periódica de lecturas

Actualización del 29/10/2026: se reparó la migración pendiente para conservar las lecturas sin nodo verificable con NodoId NULL; el constructor y el recolector siguen exigiendo nodo en nuevas muestras. Se agregaron migraciones de compatibilidad y cobertura. El diagnóstico, respaldo restaurado, comparación de las 1.578 lecturas, migraciones realmente aplicadas y límites de verificación se registran en [el informe de reparación](../Reportes%20de%20codex/reparacion_migraciones_20261029.md). El registro original siguiente conserva su alcance histórico.

Fecha de trabajo: 28/10/2026 (America/Montevideo). La carpeta de este requerimiento fue renombrada de req010 a req011 durante la implementación; se respetó el cambio. Los identificadores `req010` de los diagnósticos y escenarios de prueba conservan su referencia original.

## Cambios realizados

- Se conserva `LecturaPanel.FechaHora`, sin duplicar entidades ni tablas. Cada lectura conserva `NodoId` y navegación `Nodo` a partir del panel al medir. Las fechas se normalizan a microsegundos UTC; potencia en W, precisión decimal(18,6) y salud permanecen validadas por el dominio.
- `ConfiguracionRecoleccion` lleva el intervalo validado a Application. El simulador recibe el límite inferior de un intervalo UTC de N segundos, utilizado como instante puntual estable de la medición. Reintentos y otras instancias con el mismo N utilizan la misma clave panel/instante. No se agregaron campos redundantes al DTO existente.
- Se sustituyó la omisión por minuto por la comprobación del instante exacto. Se pueden registrar varias mediciones distintas dentro del mismo minuto.
- Se agregó el índice único `(PanelId, FechaHora)` y el índice `(NodoId, FechaHora)`. Se conserva el índice de cobertura previo `(PanelId, FechaHora, PotenciaGeneradaW)` para evitar regresiones del histórico. La fecha se materializa como UTC y se almacena en `datetime(6)`.
- El repositorio mantiene una conexión física dedicada sin pooling, intenta `GET_LOCK` antes de consultar, verifica su propiedad antes de seleccionar/guardar y aborta si perdió la conexión. Usa resolución de identidad al cargar paneles y nodos para no adjuntar varias instancias del mismo nodo al contexto. `LecturaDuplicadaException` distingue conflictos de unicidad; el lote es atómico y no sobrescribe lecturas previas.
- La liberación del bloqueo y el cierre se realizan al disponer la sesión; un error de conexión durante la liberación no oculta el error original del ciclo. El worker conserva el alcance asíncrono por ejecución, la cancelación, los ciclos secuenciales y el registro de errores con continuidad posterior.
- No se modificaron pantallas, reportes, autenticación, versiones de paquetes ni archivos de `.agents/` o `CodeQuality.Tests`.

## Archivos tocados

| Capa/etapa | Archivos |
|---|---|
| Domain | `SolarUY.Domain/Entities/LecturaPanel.cs` |
| Application | `SolarUY.Application/RecoleccionLecturas/ConfiguracionRecoleccion.cs`, `RecoleccionLecturasService.cs`, `SolarUY.Application/Exceptions/LecturaDuplicadaException.cs` |
| Infrastructure | `SolarUY.Infrastructure/Persistence/LecturaPanelConfiguration.cs`, `RecoleccionLecturasRepository.cs` |
| Migraciones | `SolarUY.Infrastructure/Migrations/20261029023745_AddRecoleccionPeriodica.cs`, su `.Designer.cs` y `ApplicationDbContextModelSnapshot.cs` |
| Presentation | `SolarUY.Presentation/BackgroundServices/RecoleccionLecturasWorker.cs`, `Program.cs`, `appsettings.json` |
| Pruebas | `SolarUY.Domain.Tests/LecturaPanelTests.cs`; `SolarUY.Integration.Tests/RecoleccionLecturasIntegrationTests.cs`, `RecoleccionProcesosIntegrationTests.cs`, `RecoleccionMigracionIntegrationTests.cs`, `HistoricoDatabase.cs`, `HistoricoGeneracionIntegrationTests.cs` |
| Documentación | `specs.md`, `tasks.md`, `implementations.md` de esta carpeta |

El escenario previo del histórico que insertaba 600 W y 1800 W para el mismo panel/instante ahora inserta una muestra de 1200 W: conserva los resultados energéticos esperados y cumple la nueva unicidad. La carga de volumen también informa el nodo obligatorio. Se mantuvieron las aserciones y límites de rendimiento. La prueba de puerto ocupado deshabilita los proveedores de logging del host de prueba para que los permisos del EventLog de Windows no oculten el error de puerto esperado.

## Configuración y ejecución

La configuración versionada es `RecoleccionLecturas:IntervaloSegundos = 60`, con `Habilitada = true`. La antigua clave `Intervalo` se reemplazó por `IntervaloSegundos`.

Ejemplo en PowerShell, suponiendo que la conexión y autenticación ya están configuradas fuera del repositorio:

```powershell
$env:RecoleccionLecturas__IntervaloSegundos = '10'
dotnet run --project SolarUY.Presentation/SolarUY.Presentation.csproj --no-launch-profile
```

El intervalo es obligatorio, entero y positivo. Se rechazan valores ausentes, vacíos, no numéricos, cero, negativos o fuera de representación. El máximo técnico es 4.294.967 segundos por el límite de `PeriodicTimer`; se admiten intervalos superiores a cinco minutos. La modificación de configuración requiere reiniciar la instancia. Todas las instancias deben usar el mismo N y relojes UTC sincronizados.

## Migración y datos anteriores

Procedimiento actualizado el 29/10/2026: detener recolectores, respaldar y verificar una restauración antes de migrar. `AddRecoleccionPeriodica` comprueba duplicados antes del DDL y agrega el nodo nullable sin completar datos históricos. Las migraciones de compatibilidad y cobertura mantienen claves foráneas y unicidad. No se borra ni consolida ninguna lectura.

En una base nueva o sin lecturas previas, se puede ejecutar `dotnet ef database update --project SolarUY.Infrastructure/SolarUY.Infrastructure.csproj` con la conexión configurada externamente.

Las filas previas conservan `NodoId = NULL`; permanecen consultables por panel y no se atribuyen a nodos/parques. La antigua variable `@SolarUY_TopologiaHistoricaVerificada` ya no autoriza rellenos masivos. Una certificación futura requiere evidencia por panel e intervalo y una operación específica, auditable y respaldada; no se ejecutó durante esta reparación. Si existen duplicados, se informa el problema antes del DDL y no se eliminan ni consolidan automáticamente. Para diagnosticarlos:

```sql
SELECT PanelId, FechaHora, COUNT(*) AS Cantidad
FROM LecturasPanel
GROUP BY PanelId, FechaHora
HAVING COUNT(*) > 1;
```

## Coordinación y recuperación

El bloqueo tiene el nombre común `CONCAT('solaruy:lecturas:', LEFT(SHA2(DATABASE(),256),40))`. Solo el propietario consulta y guarda en cada ciclo; las otras instancias vuelven a intentar después. La conexión no se recupera silenciosamente para continuar un lote que perdió la exclusión. MariaDB libera el bloqueo al cerrar o detectar la desconexión. Una partición de red puede retrasar la detección; no se promete un relevo inmediato ni recuperación de muestras de intervalos transcurridos durante una caída.

## Verificaciones reproducibles

Usar una base exclusivamente de pruebas cuyo nombre comience con `solaruy_req007_test`. Las pruebas existentes limpian sus datos, por lo que no deben apuntar a la base habitual. Guardar la conexión en `SOLARUY_HISTORICO_TEST_CONNECTION` mediante configuración local fuera del repositorio.

```powershell
dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj --no-restore -m:1
dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj --no-restore -m:1
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --no-restore -m:1
```

Para reproducir solo la recolección, agregar `--filter FullyQualifiedName~Recoleccion` al comando de integración. Para el escenario previo de rendimiento, usar `--filter FullyQualifiedName~Volumen_UnMillon --logger 'console;verbosity=normal'`.

- `RecoleccionLecturasIntegrationTests`: mediciones registradas, reintentos, intervalos menores a un minuto, exclusión entre proveedores, datos inválidos, cancelación, fallo de consulta, arranque y cierre del worker, unicidad concurrente y salud simulada.
- `RecoleccionProcesosIntegrationTests`: inicia dos procesos reales de Presentation, observa lecturas sin duplicados, termina uno y comprueba continuidad del otro; fuerza la pérdida de una conexión propietaria y comprueba rechazo de escrituras y adquisición por otra sesión. Usa un trigger temporal que rechaza inserciones para comprobar registro de fallo de guardado y recuperación al retirarlo. También ejecuta procesos con configuración inválida y válida superior a cinco minutos.
- `RecoleccionMigracionIntegrationTests`: crea bases temporales propias, aplica el esquema previo, comprueba el rechazo sin evidencia y ante duplicados, certifica la topología conocida de su escenario y comprueba conservación de datos, relaciones e índices. Elimina únicamente las bases que creó para esos escenarios.

El usuario de la base aislada necesita permisos para crear/eliminar esas bases temporales y triggers, aplicar migraciones y terminar sus conexiones de prueba. No se requieren estos privilegios ampliados para el usuario habitual de la aplicación.

## Resultados

La verificación se realizó contra un contenedor temporal MariaDB 11.8, separado de la base habitual. Para el escenario de un millón de lecturas se asignaron 512 MiB al buffer pool de InnoDB. Con la caché predeterminada de 128 MiB, el escenario anual excedió ligeramente el límite existente de cinco segundos incluso tras conservar el índice de cobertura; ese límite no se relajó.

| Verificación | Resultado |
|---|---|
| `SolarUY.Domain.Tests` | 47 aprobadas, 0 fallidas, 0 omitidas |
| `CodeQuality.Tests` | 9 aprobadas, 0 fallidas, 0 omitidas; sin cambios en sus archivos |
| Suite completa `SolarUY.Integration.Tests` | 111 aprobadas, 0 fallidas, 0 omitidas; duración 2 min 4 s |
| Pasada final de procesos e histórico conservado | 13 aprobadas, 0 fallidas, 0 omitidas; incluye una prueba nueva posterior a la suite completa y repite 12 casos de procesos; duración 29,6 s |
| Rendimiento con un millón de lecturas | Aprobado sin relajar el límite existente de 5 s por consulta ni 256 MiB adicionales en la aplicación |
| `dotnet ef migrations has-pending-model-changes` | Sin diferencias pendientes entre el modelo y la migración |
| `git diff --check` y codificación de archivos tocados | Sin errores; UTF-8 sin BOM |

La pasada final registró 117 ms para la prueba completa de pérdida de conexión/readquisición y aproximadamente 6 s para la prueba completa de dos procesos, incluyendo preparación, arranque y cierre. Son tiempos observados del escenario local, no una garantía de recuperación en producción. La prueba de continuidad exige dos lotes nuevos con N=1 s después de terminar el primer proceso y utiliza un límite de espera de 30 s.

Se verificaron 112 casos de integración distintos entre la suite completa y la prueba adicional del histórico. No quedaron verificaciones previstas omitidas. La migración se aplicó solo en bases aisladas de prueba, nunca en la base habitual. El contenedor temporal se detuvo al finalizar; Docker Desktop permanece disponible.
