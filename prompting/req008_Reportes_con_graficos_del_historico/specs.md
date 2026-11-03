# Especificaciones: Reportes con gráficos del histórico y exportación

> Reparación solicitada el 29/10/2026: reutilizar el histórico que respeta NodoId histórico y excluye lecturas legadas sin procedencia de los agregados por nodo/parque. Mostrar el aviso de lecturas sin nodo verificable y conservarlo en la exportación CSV. Las lecturas permanecen consultables por panel y nunca se reatribuyen usando la topología actual.

Fuente funcional: `requirements.md` de esta carpeta. El alcance comprende una pantalla de reportes de generación y mantenimiento, gráficos Radzen de barras y líneas, filtros y descarga CSV. Este documento define el diseño; no acredita implementación ni pruebas ejecutadas.

## Contexto, alcance y aclaraciones

### C0. Reutilización y límites

- Reutilizar `IHistoricoGeneracionService`, `FiltroHistoricoDto`, `ResultadoHistoricoDto`, `PuntoHistoricoDto` y `OpcionHistoricoDto` de req007. No duplicar SQL, integración de potencia a energía, agrupaciones, máximos/mínimos ni reglas temporales del histórico.
- Mantener .NET 8, Blazor Server, Radzen.Blazor 6.0.0, EF Core 8, Pomelo 8.0.3 y MariaDB 11.8. No incorporar bibliotecas de gráficos, CSV o pruebas adicionales ni introducir MediatR.
- Consultar el inventario persistido: parque obligatorio y nodo opcional perteneciente al parque. Sin nodo, el reporte representa el parque completo; con nodo, únicamente ese nodo. No agregar filtro por panel, comparación entre períodos, PDF ni exportación de imágenes a este requerimiento.
- La card Mantenimiento de `Home.razor` enlaza a `MantenimientoPaneles.razor`. Esta página usa `ITareaMantenimientoService` y `TareaMantenimientoDto`; no existe un componente independiente que pueda reutilizarse como resumen. Reutilizar sus registros, conceptos y campos de consulta, sin copiar el flujo de gestión al reporte.
- El servicio de mantenimiento actual permite Administrador y Tecnico, y limita al técnico a sus tareas. El criterio 8 de req008 autoriza a los tres roles a leer reportes de todos los parques: incorporar una consulta específica de solo lectura para el reporte, conservando los permisos de gestión y la página `/mantenimiento`.
- No modificar el recolector, reconstruir mediciones faltantes ni corregir como parte de este alcance el rendimiento pendiente de req007, tarea 6.8. Registrar esa dependencia al verificar la solución.

### Q1. Definición de mantenimiento realizado

Confirmado por el usuario durante la implementación:

- «Realizado» significa exclusivamente `EstadoTareaMantenimiento.Completada`, con `FechaRealizacion` dentro de `[inicioUtc, finUtc)` del reporte.
- Mostrar panel/serie, tipo, descripción, responsable, estado, fecha programada y fecha de realización.
- Excluir registros completados sin fecha de realización. No sustituirla por la fecha programada.

No agregar fechas, estados ni relaciones al dominio para resolver esta aclaración. La lectura global del resumen por los tres roles ya está autorizada por el criterio 8; no requiere otra confirmación de permisos.

### Q2. Alcance y contrato del CSV

Confirmado por el usuario durante la implementación:

- Un único archivo con generación y mantenimiento, diferenciados mediante `TipoRegistro` (`Generacion` o `Mantenimiento`).
- Separador punto y coma, UTF-8 sin BOM, fechas ISO 8601 con desplazamiento explícito de Uruguay y decimales con punto, sin redondeo adicional.
- Intervalos sin datos con energía vacía y cobertura `SinDatos`; cero medido conserva el valor cero. Se permite exportar aunque no haya mediciones ni tareas.
- Esquema técnico de columnas, en orden: `TipoRegistro`, `ParqueId`, `Parque`, `NodoId`, `Nodo`, `Periodo`, `InicioPeriodo`, `FinPeriodo`, `ZonaHoraria`, `EnergiaTotalKWh`, `CoberturaPeriodo`, `InicioIntervalo`, `FinIntervalo`, `EnergiaKWh`, `Cobertura`, `TareaId`, `PanelId`, `Panel`, `TipoMantenimiento`, `Descripcion`, `Responsable`, `Estado`, `FechaProgramada`, `FechaRealizacion`. Los metadatos de período se repiten en cada fila; las columnas de la otra clase de registro quedan vacías. Los valores se entrecomillan y las comillas internas se duplican, conservando saltos de línea y caracteres españoles.

El archivo contiene todos los intervalos, incluidos los vacíos, y únicamente los mantenimientos seleccionados por Q1. La coherencia con el resultado mostrado, el escape correcto de campos y la autorización son obligatorios.

## 1. Domain

### D1. Sin modificaciones

Reutilizar `ParqueSolar`, `Nodo`, `Panel`, `LecturaPanel`, `TareaMantenimiento` y sus estados. El reporte es una proyección de lectura, no una entidad persistida. No crear tablas de reportes, atributos, relaciones ni reglas nuevas de mantenimiento; no se necesitan migraciones ni cambios de comportamiento de Domain.

## 2. Application

### A1. Contratos y servicio de reportes

Crear el módulo `SolarUY.Application.Reportes` con `IReporteHistoricoService`/`ReporteHistoricoService`. Es necesario para coordinar generación y mantenimiento, aplicar el alcance de lectura del reporte y producir el CSV sin cálculos de negocio en la vista. Inyectar por constructor `IHistoricoGeneracionService`, `ITareaMantenimientoRepository` e `IUsuarioActual`.

Operaciones asíncronas con `CancellationToken`:

- `ListarOpcionesAsync`: delegar en el histórico y entregar únicamente parques y nodos para los filtros del reporte.
- `ConsultarAsync`: validar el filtro y devolver un resultado coherente con su parque, nodo y período efectivo.
- `ExportarCsvAsync`: autorizar nuevamente y serializar el resultado de reporte previamente obtenido en el servidor, conforme a Q2, sin volver a consultar lecturas que puedan haber cambiado desde su visualización.

Definir únicamente estos contratos de Application, sin referencias a Radzen, EF Core, HTTP ni JavaScript:

| Contrato | Contenido y responsabilidad |
| --- | --- |
| `FiltroReporteDto` | `ParqueId`, `NodoId` opcional, `PeriodoHistorico`, inicio local y fin local opcional según el período. |
| `ResultadoReporteDto` | Filtro e identificación del objetivo, `ResultadoHistoricoDto` de generación y colección de mantenimientos realizados. Conserva el resultado que alimenta los gráficos y la exportación. |
| `ConsultaMantenimientosReporteDto` | Parque, nodo opcional y límites UTC efectivos obtenidos del resultado histórico. No recibe límites recalculados por Blazor. |
| `MantenimientoRealizadoReporteDto` | Proyección de los campos existentes acordados en Q1, sin indicadores ni comandos para editar, asignar o cambiar estados. |
| `ArchivoReporteCsvDto` | Nombre de archivo, tipo de contenido CSV y contenido codificado según Q2. No expone detalles del navegador. |

Los campos de mantenimiento y el esquema del CSV son los definidos en Q1 y Q2, confirmadas por el usuario; los nombres anteriores no autorizan atributos nuevos en entidades. No crear un repositorio genérico de reportes ni un segundo servicio de cálculo del histórico.

### A2. Autorización y validación de filtros

Validar con `IUsuarioActual` una identidad autenticada con identificador no vacío y al menos uno de los roles `Administrador`, `Tecnico` o `Consulta`. Aplicar la política en opciones, consulta y exportación, también al invocar directamente el servicio. Los tres roles leen todos los parques y los mantenimientos incluidos en el reporte.

Validar parque existente, nodo existente y pertenencia del nodo al parque, identificadores no vacíos y período admitido. Resolver la relación mediante las opciones del histórico, sin usar el servicio administrativo de parques ni el listado restringido de mantenimiento. Rechazar un nodo de otro parque incluso si el identificador es válido.

Convertir el filtro a `FiltroHistoricoDto` de tipo Parque o Nodo y delegar la validación temporal en `IHistoricoGeneracionService`. Mantener sus errores diferenciados de entrada, acceso y objetivo inexistente. No copiar en el nuevo servicio los métodos privados de conversión de fechas del histórico.

### A3. Generación y mantenimiento del mismo período

Usar exactamente la serie, energía, cobertura, unidad y orden temporal devueltos por el histórico. Conservar las reglas vigentes: Uruguay (`America/Montevideo`), límites `[inicio, fin)`, vista diaria horaria, mensual diaria, anual mensual y rango personalizado diario. No reagrupar ni integrar lecturas en memoria.

Los valores `null` permanecen sin datos; el cero representa una medición igual a cero. Conservar cobertura parcial y precisión decimal; el redondeo visual a tres decimales sigue la presentación de req007 y no altera el resultado original.

Ampliar `ITareaMantenimientoRepository` con `ListarRealizadosParaReporteAsync(ConsultaMantenimientosReporteDto, CancellationToken)`, que devuelve las proyecciones de Q1. La operación se usa desde el servicio autorizado de reportes y no sustituye `ITareaMantenimientoService.ListarAsync`.

Pasar a esta consulta los límites de `ResultadoHistoricoDto.Consulta` y el mismo parque/nodo validado. No exigir que existan lecturas para consultar los mantenimientos: puede haber mantenimiento en un período sin generación registrada. No deducir tareas desde lecturas, estados del simulador o fechas de programación si Q1 no lo autoriza.

### A4. Coherencia y exportación

Los gráficos y el CSV se construyen con el mismo `ResultadoReporteDto` aceptado por la pantalla. El cambio entre barras y líneas no ejecuta una nueva consulta ni cambia los datos. El CSV incluye todos los intervalos del resultado, no solo los puntos visibles o las filas de una página.

La vista conserva el resultado en el servidor y lo entrega al caso de uso de exportación; no acepta filas o totales del navegador como fuente del reporte. La exportación vuelve a comprobar identidad/rol y valida el resultado y sus metadatos antes de serializar. No reconsultar automáticamente al exportar: el recolector podría haber agregado lecturas y producir discrepancias con el gráfico.

Aplicar el contrato acordado en Q2, escape del separador, comillas, saltos de línea y caracteres españoles, con nombre de archivo representativo del objetivo y período. No escribir archivos permanentes en el servidor ni agregar librerías de CSV. Si se cambia el filtro, no habilitar la exportación del resultado anterior bajo los filtros nuevos.

## 3. Infrastructure

### I1. Consulta de mantenimientos sobre los registros existentes

Implementar la nueva operación en `TareaMantenimientoRepository`, reutilizando `IDbContextFactory<ApplicationDbContext>` y las relaciones `TareaMantenimiento.Panel.Nodo.Parque`. Usar un contexto por operación, lectura sin seguimiento y cancelación.

Aplicar en MariaDB los filtros por parque, nodo opcional, estado y fecha acordados en Q1. Proyectar únicamente los campos del resumen y ordenar de forma estable por fecha de realización e identificador. No cargar todo el historial de mantenimiento para filtrarlo en Application y no realizar una consulta por panel o tarea.

Esta lectura tiene el alcance global autorizado por A2. Mantener sin cambios las restricciones por responsable y las validaciones de las operaciones de gestión existentes. No añadir escrituras, consultas al directorio de Keycloak para completar campos no acordados ni tablas auxiliares.

### I2. Sin duplicación del histórico ni infraestructura de exportación

Reutilizar `HistoricoGeneracionRepository` a través de su servicio existente, sin modificar su SQL o índices para implementar gráficos. No añadir un repositorio de exportaciones, almacenamiento de archivos, caché compartida, trabajos periódicos ni migraciones. El CSV es una transformación de resultados en Application; su descarga corresponde al host y navegador.

## 4. Presentation

### P1. Página, navegación y composición

Crear `ReportesHistorico.razor` en `/reportes`, protegida para `Administrador,Tecnico,Consulta`, e incorporar el acceso al menú existente. Registrar `IReporteHistoricoService` en `Program.cs` con el patrón de ámbito e identidad del circuito utilizado por el histórico. No acceder desde la página a Infrastructure, repositorios ni `DbContext`.

Permitir seleccionar parque, nodo opcional del parque, período Diario/Mensual/Anual/Rango y fechas correspondientes. Cargar opciones desde el servicio de reportes. Al cambiar de parque, limpiar un nodo que ya no pertenezca a él; conservar siempre una selección válida.

### P2. Gráficos Radzen y disponibilidad

Utilizar `RadzenChart` con representaciones de barras y líneas de la misma serie. Proveer un selector de representación para visualizar ambas sin alterar el filtro o el resultado. Eje temporal con las etiquetas del histórico y eje de energía rotulado en kWh; mostrar objetivo, período efectivo y zona horaria.

No presentar energía faltante como cero ni unir visualmente tramos de una línea atravesando intervalos sin datos. Verificar cómo admite ausencias la API de Radzen.Blazor 6.0.0 instalada: usar valores ausentes o segmentos separados, conservando el orden y los intervalos originales. Si se necesita adaptar la colección para el componente, preparar los DTO en Application sin recalcular energía ni interpolar valores. No cambiar la versión del paquete para resolverlo.

Identificar los intervalos sin datos y la cobertura parcial mediante mensajes/leyenda o detalle accesible del resultado, incluso si no generan una barra o punto. Mostrar cero medido como valor válido. Conservar el total de energía observada y el aviso de cobertura parcial existentes, sin añadir indicadores no solicitados.

### P3. Actualización y estados

Consultar al cambiar un filtro válido y actualizar generación y mantenimiento juntos para la selección vigente. Cancelar consultas anteriores cuando sea posible y descartar respuestas tardías aunque el proveedor no haya atendido la cancelación. Cancelar operaciones al desechar el componente.

Mostrar carga, filtros inválidos, acceso denegado, inexistencia, falta de lecturas, falta de mantenimiento y errores de consulta/descarga. Durante carga o tras un cambio/error, no mostrar datos previos como si correspondieran a los filtros actuales ni permitir exportarlos. La recolección no implica actualización automática del reporte en tiempo real.

### P4. Resumen de mantenimiento y descarga

Mostrar los mantenimientos de `ResultadoReporteDto` con los campos acordados en Q1, reutilizando la presentación y terminología del módulo existente cuando corresponda. Explicitar la zona horaria de las fechas mostradas y usar el mismo período del reporte. No renderizar acciones de gestión ni ampliar el acceso a `/mantenimiento` para Consulta.

El botón Exportar CSV invoca el caso de uso con el resultado vigente. Descargar el archivo mediante interoperabilidad Blazor/JavaScript del proyecto y APIs del navegador, sin paquetes nuevos ni endpoints públicos de exportación. El código del navegador solo recibe el contenido autorizado, inicia la descarga y libera los recursos temporales; no genera cálculos ni selecciona datos del reporte. El alcance exacto del archivo depende de Q2.

## 5. Verificación

### V1. Integración y regresión

Agregar casos en `SolarUY.Integration.Tests` con servicios y repositorios reales, MariaDB 11.8 aislada, identidad controlada y datos conocidos de al menos dos parques y sus nodos. Reutilizar el mecanismo externo de conexión de req007 y la serialización de pruebas que preparan la misma base; no cargar ni borrar datos habituales. No agregar pruebas unitarias de Application, Infrastructure o Presentation. Domain no cambia.

Comprobar:

- Consultas diarias, mensuales, anuales y por rango para parque completo y nodo, límites UTC/Uruguay y coincidencia exacta con `IHistoricoGeneracionService` para el mismo filtro.
- Exclusión de otro parque/nodo, rechazo de pertenencia inválida y objetivos inexistentes, rangos inválidos y cancelación.
- Energía positiva, cero medido, huecos y cobertura parcial sin sustitución de `null` ni redondeo intermedio.
- Mantenimientos según Q1, incluidos límites del período, tareas de otros objetivos y estados excluidos; caso sin lecturas pero con mantenimiento. No usar la fecha programada como sustituto implícito.
- Lectura y exportación globales de los tres roles, denegación a identidades anónimas/sin rol admitido y conservación de los permisos de gestión de mantenimiento.
- CSV según Q2: datos/intervalos/metadatos del resultado, precisión acordada, ausencia de datos, caracteres especiales y escape. Después de generar un reporte, agregar una lectura en la base aislada y verificar que la exportación conserva los valores del resultado mostrado.
- Consultar y exportar no modifican lecturas ni tareas persistidas.

### V2. Verificación de la pantalla

Recorrer `/reportes` con los tres roles y datos conocidos, usando el navegador disponible, sin introducir dependencias de pruebas nuevas. Verificar barras y líneas, etiquetas/unidad, huecos sin interpolación, cero medido, cobertura, filtros encadenados, períodos, resumen de mantenimiento y archivo descargado. Comparar los valores visibles y el CSV con el mismo resultado del histórico.

Comprobar cambios rápidos de filtros con respuestas fuera de orden, estados de carga/error, exportación deshabilitada cuando el resultado no está vigente y cambio de tipo de gráfico sin alterar sus valores. Registrar el método utilizado y cualquier caso no ejecutado.

### V3. Guardarraíles y pruebas existentes

Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` tras los cambios de código, corrigiendo el código ante fallos sin modificar los tests arquitectónicos. Ejecutar las nuevas integraciones y `dotnet test SolarUY.sln` con la conexión aislada configurada para evitar omisiones de MariaDB. No crear tests de Domain sin un cambio de comportamiento autorizado.

Registrar comandos, configuración no secreta, resultados, omisiones y fallos. La prueba de volumen de req007 tiene un incumplimiento previamente documentado (5,487 s en parque/mes frente a 5 s); comprobar y registrar su estado sin relajar el umbral ni declarar aprobada la solución si persiste el fallo. No añadir a req008 nuevos compromisos de rendimiento que no estén en sus requisitos.

## 6. Trazabilidad y cierre

| Criterio de aceptación | Especificación |
| --- | --- |
| 1. Pantalla con barras y líneas Radzen | P1, P2, V2 |
| 2. Histórico diario, mensual y anual reutilizable | A1, A3, P1, V1 |
| 3. Filtros por parque, nodo y fechas | A2, A3, I1, P1, V1 |
| 4. Actualización al modificar filtros | A4, P3, V2 |
| 5. Exportación CSV | Q2, A4, P4, V1, V2 |
| 6. Mantenimientos realizados del período | Q1, A3, I1, P4, V1 |
| 7. Consistencia con el histórico | A3, A4, P2, V1, V2 |
| 8. Tres roles, todos los parques | A2, I1, P1, P4, V1, V2 |

### C1. Documentación de implementación

La última tarea crea `implementations.md` en esta carpeta con cambios realizados, archivos tocados y resultados de las pruebas. Registrar las respuestas a Q1/Q2 en esta especificación antes de cerrar sus tareas dependientes. Actualizar `tasks.md` solo con evidencia de ejecución y declarar los bloqueos o verificaciones pendientes.
