# Implementación: Reportes con gráficos del histórico y exportación

Fecha: 22/10/2026. La pantalla está disponible en `/reportes` para Administrador, Tecnico y Consulta. Reutiliza el histórico de req007, agrega el resumen de mantenimiento realizado y permite descargar ambos conjuntos de datos en un único CSV.

## Comportamiento implementado

- Filtros por parque, nodo opcional, período diario/mensual/anual o rango y fechas de Uruguay. Los cambios consultan automáticamente; cambiar de parque limpia la selección de nodo. La pertenencia se valida también en Application.
- Gráficos Radzen de barras y líneas sobre los valores del histórico, con unidad kWh, eje temporal de Uruguay y escala vertical legible para valores menores a 1 kWh. Cambiar la representación no consulta de nuevo.
- Los puntos sin energía se conservan como ausencia de datos en el detalle y no se convierten en cero. Las líneas se separan en segmentos para no atravesar huecos; se conservan los ceros medidos y los avisos de cobertura parcial.
- Mantenimientos exclusivamente Completados cuya FechaRealizacion pertenece a `[inicio, fin)`. Se excluyen completados sin fecha; se muestran panel, tipo, descripción, responsable, estado y las dos fechas. Los campos proceden de los registros existentes, sin duplicarlos.
- CSV con separador punto y coma, UTF-8 sin BOM, fechas ISO 8601 con desplazamiento de Uruguay y decimales invariantes sin redondeo adicional. `TipoRegistro` distingue Generacion/Mantenimiento; incluye todos los intervalos, incluso los que tienen energía vacía y cobertura SinDatos. Los encabezados y sus 24 columnas están registrados en Q2 de `specs.md`.
- Exportación del mismo resultado generado en el servidor que alimenta la pantalla, con nueva comprobación de identidad y rol. No vuelve a consultar las lecturas al descargar: el archivo conserva los valores aunque el recolector haya guardado muestras nuevas.
- Carga, validaciones, inexistencia, denegación, ausencia de energía/tareas y errores visibles. Los filtros nuevos invalidan el resultado previo y deshabilitan su exportación. La cancelación y un contador de versión impiden que una respuesta tardía sustituya a la selección vigente.

Q1 y Q2 fueron respondidas por el usuario durante la implementación y sus respuestas quedaron registradas en `specs.md`.

## Cambios por capa y archivos

Domain no cambia: no se añadieron entidades, atributos, estados, relaciones ni migraciones.

Application:

- `SolarUY.Application/Reportes/FiltroReporteDto.cs`: filtro de parque/nodo, período y fechas.
- `ContratosReportes.cs`: contratos del servicio, consulta y resumen de mantenimiento, archivo CSV y resultado de reporte con colecciones de solo lectura.
- `ReporteHistoricoService.cs`: autorización, opciones, pertenencia parque/nodo y coordinación del histórico con mantenimiento. Delega toda la energía y resolución temporal en `IHistoricoGeneracionService`.
- `GraficoReporteDto.cs`: adaptación de puntos y segmentos para Radzen, conservando los valores originales.
- `ReporteCsv.cs`: serialización y escape del resultado, sin archivos persistentes en servidor ni bibliotecas adicionales.
- `SolarUY.Application/Mantenimiento/ITareaMantenimientoRepository.cs`: nueva operación específica de consulta para reportes.

Infrastructure:

- `SolarUY.Infrastructure/Persistence/TareaMantenimientoRepository.cs`: nueva proyección SQL de solo lectura, con filtros de parque/nodo, estado y fecha de realización, contexto por operación, cancelación y orden estable.
- Las restricciones por responsable y las operaciones de gestión de mantenimiento mantienen su comportamiento. No se modificaron el SQL del histórico, índices, recolector ni configuración de despliegue.

Presentation:

- `SolarUY.Presentation/Components/Pages/ReportesHistorico.razor`: filtros, estado de consulta, resumen, exportación y manejo de concurrencia.
- `Components/Shared/GraficoGeneracionReporte.razor`: gráficos Radzen con selector de representación.
- `Components/Layout/NavMenu.razor`: acceso a Reportes para los tres roles.
- `Program.cs`: registro del servicio de reportes con el ámbito de circuito existente.
- `wwwroot/js/reportes.js`: descarga del contenido autorizado y liberación de la URL temporal del navegador.

Verificación y documentación:

- `SolarUY.Integration.Tests/ReportesIntegrationTests.cs`: 27 casos de servicios/repositorios con MariaDB real.
- `ReportesConcurrenciaIntegrationTests.cs`: un caso del componente real y HtmlRenderer con servicios y persistencia reales.
- `SolarUY.Integration.Tests/README.md`: preparación y ejecución de los 28 casos.
- `specs.md`, `tasks.md` y este informe en la carpeta de req008. `requirements.md` se conserva sin cambios.

## Verificaciones

### Integración de reportes

Los **28 casos nuevos pasan**, sin omisiones. Cubren los cuatro períodos por parque/nodo, valores conocidos y comparación con el histórico, cero/huecos, límites y pertenencia, mantenimiento por fecha de realización, estados excluidos, datos inconsistentes, falta de lecturas, roles y restricciones de gestión, CSV/escape/precisión, conservación de datos y cancelación.

La prueba de concurrencia retiene la respuesta de una consulta real y devuelve primero otra consulta posterior. El servicio intervenido ignora la cancelación de la primera. Se envían los callbacks mediante el mecanismo de eventos de Blazor y se comprueba el HTML del componente: el resultado más reciente no cambia al llegar la respuesta anterior. No son pruebas unitarias de Presentation ni requieren paquetes nuevos.

```powershell
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --no-restore -m:1 --disable-build-servers -p:BaseOutputPath=bin/req008/ --filter 'FullyQualifiedName~Reportes' --logger trx --results-directory artifacts/req008/reportes-final
```

### Regresión funcional y arquitectura

Se configuró externamente `SOLARUY_HISTORICO_TEST_CONNECTION` con una base exclusiva `solaruy_req007_test_reportes` en MariaDB 11.8, puerto local 3307. La compilación se realizó en una salida separada para no bloquear los binarios de la aplicación habitual.

```powershell
dotnet test SolarUY.sln --no-restore -m:1 --disable-build-servers -p:BaseOutputPath=bin/req008/ --filter 'FullyQualifiedName!~Volumen' --logger trx --results-directory artifacts/req008/funcionales-final
```

| Proyecto | Correctas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| CodeQuality.Tests | 9 | 0 | 0 |
| SolarUY.Domain.Tests | 46 | 0 | 0 |
| SolarUY.Application.Tests | 8 | 0 | 0 |
| SolarUY.Integration.Tests, excepto volumen | 88 | 0 | 0 |
| **Total** | **151** | **0** | **0** |

Los controles arquitectónicos se ejecutaron después de los cambios, sin modificar sus archivos. No se añadieron pruebas unitarias de las capas externas, dependencias o versiones nuevas.

### Solución completa y rendimiento previamente pendiente

También se ejecutó `dotnet test SolarUY.sln` con los mismos parámetros de compilación e integración, sin excluir volumen. En ese momento, antes de agregar el caso de concurrencia, el resultado fue **150 correctas y un fallo, sin omisiones**. El fallo es el escenario existente de req007 con un millón de lecturas y cuatro consultas concurrentes:

- Parque/día: máximo 1,731 s; memoria adicional máxima de aplicación 3,87 MiB.
- Parque/mes: máximo **5,685 s**, frente al umbral de 5 s; memoria adicional máxima de aplicación 0,85 MiB.

El escenario se detiene al fallar el criterio mensual; no se declaran aprobados los escenarios posteriores. La tarea 6.8 de req007 sigue pendiente. No se cambió su consulta ni se relajó el umbral, y no se presenta la solución completa como aprobada. Evidencia: `artifacts/req008/solucion/`.

### Navegador y descarga

Se usaron Edge sin ventana, Keycloak 26.8.1 y MariaDB aislados, con usuarios ficticios para los tres roles y uno sin permiso. La aplicación de prueba utiliza HTTPS 7288 y tiene la recolección deshabilitada, sin acceder a la base habitual.

El recorrido final terminó con **44 comprobaciones correctas, cero fallos y cero excepciones JavaScript**. Los datos conocidos muestran 0,880 kWh diarios por parque, 0,580 kWh por nodo y 0,720 kWh anuales para ese nodo. Se verificaron barras/líneas, huecos, cero medido, cambio de representación, filtros, mantenimiento, validación de rangos, período vacío, CSV realmente descargado y denegación de gestión a Consulta. Se revisaron capturas de escritorio y móvil.

Evidencia y utilidades locales en `artifacts/req008/`: `browser.mjs`, `browser-results.json`, capturas, descargas, resultados TRX, preparador de datos y registros del host. La prueba de respuestas fuera de orden se acredita adicionalmente mediante la integración controlada descrita arriba.

Al cerrar la verificación se detuvieron la aplicación HTTPS 7288 y Edge de prueba, y se eliminaron únicamente los contenedores aislados creados para req008. Se comprobaron libres los puertos 7288, 9238, 3307 y 8082. No se modificaron los servicios habituales, credenciales, datos de la base local ni procesos de la aplicación en 5219.

## Uso

Recompilar y reiniciar la aplicación habitual, luego abrir `/reportes` o el nuevo enlace Reportes del menú. Se reutilizan las migraciones de req007 ya existentes; esta entrega no requiere una migración nueva. La disponibilidad de energía depende de las lecturas persistidas y de la cobertura del período seleccionado.
