# Especificaciones: REQ015 — Reportes con gráficos del histórico y exportación

## C0. Fuente, alcance y estado de partida

Fuente funcional: [requirements.md](requirements.md) de esta carpeta, correspondiente a la Entrega 3. Este documento define el resultado técnico esperado; no acredita implementación ni ejecución de pruebas.

REQ015 se apoya en funcionalidades ya presentes de req007 (histórico), req004 (mantenimiento) y req008 (reportes). Reutilizar y verificar esa implementación; ajustar únicamente las diferencias demostradas respecto de estos requisitos. No crear otra pantalla de reportes ni duplicar contratos, repositorios o cálculos porque cambie el número del requerimiento.

Referencias consultadas: `.agents/rules/`, las especificaciones de req007, req004 y req008, y el código vigente de los módulos `HistoricoGeneracion`, `Mantenimiento` y `Reportes`. Las decisiones de mantenimiento realizado y formato CSV ya están documentadas como confirmadas en req008; se conservan como comportamiento existente, sin atribuir nuevas confirmaciones a REQ015.

| Elemento existente | Reutilización prevista |
| --- | --- |
| `SolarUY.Application.HistoricoGeneracion.IHistoricoGeneracionService` | Opciones, validación temporal y generación histórica calculada. |
| `SolarUY.Application.Reportes.IReporteHistoricoService` / `ReporteHistoricoService` | Autorización, validación del objetivo y coordinación de generación, mantenimiento y CSV. |
| `FiltroReporteDto`, `ResultadoReporteDto`, `GraficoReporteDto`, `ArchivoReporteCsvDto` | Filtro, resultado conservado en servidor, representación gráfica y descarga. |
| `ITareaMantenimientoRepository.ListarRealizadosParaReporteAsync` y `TareaMantenimientoRepository` | Consulta de registros de mantenimiento para el reporte. |
| `ReportesHistorico.razor`, `GraficoGeneracionReporte.razor`, `ResultadoHistorico.razor` | Página `/reportes`, gráficos Radzen y detalle del histórico. |
| `SolarUY.Presentation/wwwroot/js/reportes.js` | Descarga del archivo autorizado mediante interoperabilidad Blazor/JavaScript. |
| `ReportesIntegrationTests`, `ReportesConcurrenciaIntegrationTests`, `ProcedenciaHistoricaIntegrationTests` | Cobertura existente que debe evaluarse antes de añadir escenarios faltantes. |

Conservar .NET 8, Blazor Server, Radzen.Blazor 6.0.0 instalado, EF Core 8, Pomelo 8.0.3, MariaDB 11.8 y Keycloak mediante OpenID Connect. No introducir paquetes ni patrones nuevos como MediatR. El alcance es de consulta y exportación: no incluye comparación de períodos, filtros por panel, PDF, imágenes, modificaciones del recolector ni actualización de reportes mediante SignalR.

## 1. Domain

### D1. Modelo existente y ausencia de escrituras

Reutilizar `ParqueSolar`, `Nodo`, `Panel`, `LecturaPanel`, `TareaMantenimiento` y los estados existentes. Un reporte es una proyección de lectura; no requiere entidad persistida, atributos, relaciones ni cambios de comportamiento del dominio.

Consultar y exportar no deben modificar lecturas, tareas ni inventario. No se requieren migraciones. No crear comportamiento de dominio para resolver presentación de gráficos o serialización CSV.

### D2. Procedencia histórica

Mantener la procedencia por nodo registrada en cada lectura histórica. Los agregados por nodo/parque deben conservar la semántica del histórico existente: no utilizar la ubicación actual del panel para reatribuir lecturas anteriores y excluir de esos agregados las lecturas sin nodo histórico verificable. Estas lecturas siguen conservadas y consultables por panel en el módulo histórico; REQ015 no añade ese filtro al reporte.

Conservar `LecturasSinNodoHistorico` y su aviso en pantalla/CSV. En consultas por nodo/parque este diagnóstico abarca la base durante el período, tal como informa el histórico; no presentarlo como una cantidad atribuida al objetivo seleccionado ni sumar su energía al reporte.

## 2. Application

### A1. Contratos y coordinación

Reutilizar `SolarUY.Application.Reportes` y sus operaciones asíncronas con `CancellationToken`:

- `ListarOpcionesAsync`: autorizar y delegar en el histórico, exponiendo parques y nodos para los filtros.
- `ConsultarAsync(FiltroReporteDto)`: validar el objetivo, consultar generación y obtener los mantenimientos del mismo período efectivo.
- `ExportarCsvAsync(ResultadoReporteDto)`: volver a autorizar y serializar el resultado vigente conservado en el servidor.

Mantener la inyección por constructor de `IHistoricoGeneracionService`, `ITareaMantenimientoRepository` e `IUsuarioActual` en `ReporteHistoricoService`. Application depende solo de Domain; los contratos no conocen EF Core, Radzen, Blazor, JavaScript ni HTTP.

`FiltroReporteDto` conserva parque obligatorio, nodo opcional, `PeriodoHistorico`, `InicioLocal` y `FinLocal` opcional según el período. `ResultadoReporteDto` conserva el filtro, los nombres de parque/nodo, `ResultadoHistoricoDto`, los mantenimientos y `GraficoReporteDto`, con colecciones de solo lectura. El resultado que alimenta el gráfico también es la fuente de exportación.

### A2. Autorización y validación de objetivos

En opciones, consulta y exportación exigir una identidad autenticada, con identificador no vacío y al menos uno de los roles `Administrador`, `Tecnico` o `Consulta`. Aplicar el control en el servicio, además de la protección de la página.

Los tres roles pueden leer y exportar los reportes de los parques y nodos disponibles, incluido el resumen de mantenimiento. Conservar los permisos de gestión de mantenimiento y sus restricciones por responsable; la lectura del reporte no habilita editar tareas ni amplía el acceso a `/mantenimiento`.

Validar identificadores, período admitido, existencia del parque/nodo y pertenencia del nodo al parque mediante las opciones del histórico. Sin nodo se consulta el parque completo; con nodo se consulta exclusivamente ese nodo. Rechazar un nodo de otro parque. Conservar errores diferenciados de entrada inválida, falta de permisos y objetivo inexistente.

### A3. Períodos y consistencia de generación

Convertir el filtro a `FiltroHistoricoDto` con objetivo Parque o Nodo y delegar en `IHistoricoGeneracionService.ConsultarAsync`. No duplicar integración de potencia a energía, SQL, agrupación, validación de fechas ni conversión de zona horaria.

La consulta histórica existente interpreta las fechas en `America/Montevideo`, almacena y consulta límites UTC y usa intervalos `[inicio, fin)`, con fin exclusivo. Reutilizar su contrato temporal:

| Período | Selección efectiva | Serie entregada por el histórico |
| --- | --- | --- |
| `Diario` | Día de la fecha seleccionada. | Intervalos horarios del día. |
| `Mensual` | Mes de la fecha seleccionada. | Intervalos diarios del mes. |
| `Anual` | Año de la fecha seleccionada. | Intervalos mensuales del año. |
| `Rango` | Inicio y fin local explícitos, con inicio anterior al fin. | Intervalos diarios recortados a los límites solicitados. |

La consulta por día, mes y año conserva las vistas y totales del histórico; no introduce otra regla de agrupación. Validar las fechas mediante el servicio existente, incluidas fechas fuera de rango y horas locales ambiguas/inexistentes. La UI solo captura las fechas y presenta sus errores.

Mantener exactamente la unidad kWh, energía total, serie ordenada, intervalos, cobertura y precisión decimal devueltos. `null` significa ausencia de datos y no se sustituye por cero. Un cero medido es un valor válido. Conservar los avisos de cobertura parcial y de procedencia desconocida. El formato visual de tres decimales no cambia los valores originales ni el CSV.

`GraficoReporteDto.DesdeHistorico` adapta los puntos existentes sin recalcular energía: mantiene los ceros, omite puntos sin datos de la representación y separa los segmentos de línea en los huecos. El detalle conserva todos los intervalos, también los vacíos.

### A4. Resumen de mantenimiento

Reutilizar los registros y campos del módulo al que accede la card Mantenimiento de `Home.razor`; la card enlaza a `/mantenimiento` y no es un componente independiente de resumen. No copiar el flujo de gestión a reportes.

Usar `ITareaMantenimientoRepository.ListarRealizadosParaReporteAsync` con `ConsultaMantenimientosReporteDto`, pasando el parque/nodo validado y los límites UTC de `ResultadoHistoricoDto.Consulta`. No volver a calcular el período en la vista ni exigir que existan mediciones para mostrar tareas.

Conservar la definición existente de realizado: `EstadoTareaMantenimiento.Completada`, `FechaRealizacion` no nula y dentro de `[InicioUtc, FinUtc)`. No sustituirla por `FechaProgramada`. Mostrar los campos existentes de `MantenimientoRealizadoReporteDto`: panel/número de serie, tipo, descripción, responsable, estado, fecha programada y fecha de realización. El responsable es `ResponsableId`; no se necesita otra consulta a Keycloak.

El objetivo de mantenimiento se filtra mediante las relaciones persistidas tarea → panel → nodo → parque que ya usa el módulo. No inventar procedencia histórica de tareas ni nuevas relaciones para equipararla a la procedencia de lecturas.

### A5. Exportación CSV del resultado mostrado

Reutilizar `ReporteCsv` y `ArchivoReporteCsvDto`. Exportar el mismo `ResultadoReporteDto` vigente en el servidor, con sus metadatos y todos sus intervalos y mantenimientos, aunque el detalle esté paginado. No volver a consultar las lecturas al descargar, ya que podrían haberse incorporado mediciones desde que se dibujó el gráfico.

Volver a comprobar identidad y roles, y conservar la validación de coherencia entre filtro, objetivo y período del resultado. No aceptar filas ni totales construidos por el navegador. Al cambiar filtros o producirse una consulta fallida, el resultado anterior deja de ser exportable bajo la selección nueva.

Mantener el formato existente: un archivo con `TipoRegistro` igual a `Generacion`, `Mantenimiento` o `Advertencia` cuando corresponda el aviso de procedencia; separador punto y coma; UTF-8 sin BOM; terminación de fila CRLF; fechas ISO 8601 con desplazamiento explícito de Uruguay; números con cultura invariante y sin redondeo adicional. Entrecomillar los campos y duplicar comillas internas, conservando separadores, saltos de línea y caracteres españoles.

Conservar las 24 columnas existentes, en este orden:

`TipoRegistro`, `ParqueId`, `Parque`, `NodoId`, `Nodo`, `Periodo`, `InicioPeriodo`, `FinPeriodo`, `ZonaHoraria`, `EnergiaTotalKWh`, `CoberturaPeriodo`, `InicioIntervalo`, `FinIntervalo`, `EnergiaKWh`, `Cobertura`, `TareaId`, `PanelId`, `Panel`, `TipoMantenimiento`, `Descripcion`, `Responsable`, `Estado`, `FechaProgramada`, `FechaRealizacion`.

Repetir los metadatos del reporte por fila y dejar vacías las columnas que no corresponden a su tipo. En intervalos sin datos, energía vacía y cobertura `SinDatos`; un cero medido se exporta como cero. Permitir exportar un reporte válido sin mediciones ni tareas. Conservar nombre representativo del objetivo/período y tipo `text/csv;charset=utf-8`, sin persistir archivos en el servidor.

## 3. Infrastructure

### I1. Consultas existentes y aislamiento de datos

Reutilizar `HistoricoGeneracionRepository` mediante su servicio, sin introducir otro repositorio de reportes ni consultas alternativas para los gráficos. Conservar sus reglas de procedencia histórica y no modificar lecturas para completar huecos.

Reutilizar `TareaMantenimientoRepository.ListarRealizadosParaReporteAsync`: contexto por operación mediante `IDbContextFactory<ApplicationDbContext>`, `AsNoTracking`, filtros en MariaDB por parque, nodo opcional, estado y fecha de realización, proyección de campos del resumen y orden estable por `FechaRealizacion` e identificador. Propagar cancelación. Evitar cargar todas las tareas para filtrarlas en Application o realizar una consulta por panel.

No modificar las operaciones de gestión de mantenimiento ni agregar migraciones, índices, tablas auxiliares, caché, almacenamiento de exportaciones o trabajos periódicos para este requerimiento. La única adaptación de persistencia admisible es corregir una diferencia de la consulta existente respecto de A4.

## 4. Presentation

### P1. Página, navegación y composición

Reutilizar `/reportes` en `SolarUY.Presentation/Components/Pages/ReportesHistorico.razor`, su acceso en `NavMenu.razor` y el registro de `IReporteHistoricoService` en `Program.cs`. Mantener `[Authorize(Roles = "Administrador,Tecnico,Consulta")]` y la visibilidad del acceso para esos roles. La composición de dependencias permanece en el arranque; los componentes no acceden a Infrastructure, repositorios ni `DbContext`.

Ofrecer parque obligatorio, nodo opcional perteneciente al parque, período Diario/Mensual/Anual/Rango y sus fechas. Cargar opciones mediante el servicio de reportes. Al cambiar de parque, limpiar el nodo anterior y actualizar sus opciones. Explicar que las fechas corresponden a Uruguay y que el fin del rango es exclusivo.

### P2. Barras, líneas y detalle

Reutilizar `GraficoGeneracionReporte.razor` con `RadzenChart`, `RadzenColumnSeries` y `RadzenLineSeries`. El selector Barras/Líneas cambia solo la representación, sin consultar otra vez ni alterar valores. Consumir los puntos y segmentos preparados por Application.

Mostrar eje temporal, energía en kWh, objetivo, período efectivo y zona horaria. No conectar líneas a través de intervalos sin datos ni representar ausencias como cero. Reutilizar `ResultadoHistorico.razor` para total, intervalos, cobertura, valores y avisos de procedencia. Mantener el mensaje explícito cuando no hay energía observada.

### P3. Actualización y estados de pantalla

Consultar al cambiar filtros o período válidos y sustituir generación y mantenimiento como un único resultado. Invalidar el resultado anterior al empezar una nueva consulta. Cancelar consultas anteriores y descartar respuestas tardías mediante la versión de selección existente, incluso si el proveedor ignora la cancelación. Cancelar operaciones al desechar la página.

Mostrar estados de carga, fechas/filtros inválidos, acceso denegado, objetivo inexistente, falta de parques, falta de generación, falta de mantenimiento y errores de consulta/descarga. Deshabilitar exportación mientras no exista un resultado vigente, durante carga o descarga. No mostrar resultados anteriores como correspondientes al nuevo filtro.

### P4. Resumen y descarga

Mostrar los mantenimientos de `ResultadoReporteDto` con los campos de A4 y fechas de Uruguay, con mensaje propio cuando la colección está vacía, independientemente de que haya generación. Reutilizar terminología y datos existentes, sin acciones de gestión.

Asignar anchos de columna que conserven legibles los campos del resumen. En ventanas angostas, permitir el desplazamiento horizontal de la grilla para acceder a las columnas sin comprimir simultáneamente todos sus encabezados y valores.

El botón Exportar CSV invoca `ExportarCsvAsync` con el resultado vigente. Reutilizar `reportes.js` para descargar el contenido autorizado con las APIs del navegador y liberar sus recursos temporales. Si los filtros cambian durante la exportación, descartar la descarga correspondiente a la selección anterior. No generar datos del reporte en JavaScript ni crear un endpoint público adicional.

## 5. Verificación

### V1. Integración funcional

Evaluar primero la cobertura de `SolarUY.Integration.Tests/ReportesIntegrationTests.cs`, `ReportesConcurrenciaIntegrationTests.cs`, `HistoricoGeneracionIntegrationTests.cs` y `ProcedenciaHistoricaIntegrationTests.cs`. Añadir únicamente escenarios faltantes de los siguientes comportamientos, mediante integración de componentes reales y las herramientas existentes; no crear pruebas unitarias de Application, Infrastructure ni Presentation. Domain no cambia y no requiere nuevas pruebas unitarias.

- **V1.1.** Consultas Diario/Mensual/Anual/Rango, por parque y nodo; igualdad de energía, intervalos, cobertura y precisión con la consulta histórica equivalente; límites UTC/Uruguay.
- **V1.2.** Aislamiento entre al menos dos parques y sus nodos, rechazo de nodo ajeno, objetivos inexistentes, identificadores/períodos inválidos y rangos incorrectos.
- **V1.3.** Cero medido, huecos y cobertura parcial; segmentos sin interpolar ausencias; lecturas de nodo desconocido excluidas de agregados y traslado de panel sin reatribuir lecturas verificadas. Aviso de procedencia conservado en reporte y CSV.
- **V1.4.** Mantenimiento completado por fecha de realización, límites inclusivo/exclusivo, exclusión de estados y objetivos ajenos, completadas sin fecha, reporte sin lecturas pero con mantenimiento y sin datos de ambos tipos.
- **V1.5.** Opciones, lectura y exportación para los tres roles; denegación a anónimo, identidad inválida y roles ajenos; conservación de restricciones de gestión de mantenimiento.
- **V1.6.** CSV equivalente al resultado completo mostrado: columnas, metadatos, fechas, precisión, cero frente a vacío, caracteres especiales y avisos. Incorporar una lectura después de consultar y comprobar que exportar conserva el resultado anterior. Consultar/exportar no alteran registros persistidos.
- **V1.7.** Cambios rápidos de filtros con respuestas fuera de orden, invalidación de resultados anteriores y propagación de cancelación, reutilizando el escenario de concurrencia de la página real.

Usar MariaDB 11.8 aislada mediante `SOLARUY_HISTORICO_TEST_CONNECTION`, con base cuyo nombre comience con `solaruy_req007_test`, y la colección existente sin paralelismo. Nunca apuntar las pruebas a la base habitual. Si no está configurada la conexión, registrar las omisiones y no declarar validada la integración MariaDB.

Para reproducir también la cancelación demorada sin depender de la disponibilidad de MariaDB, mantener una variante complementaria que integre la página, Radzen, `HistoricoGeneracionService` y `ReporteHistoricoService` reales, sustituyendo únicamente las interfaces de repositorio por respuestas controladas. Esta variante no verifica SQL ni sustituye el mismo escenario con repositorios reales y MariaDB.

### V2. Comprobación de interfaz

Recorrer `/reportes` con datos conocidos y los tres roles, usando el navegador y las herramientas disponibles sin agregar dependencias. Verificar barras/líneas, ejes y kWh, total y detalle, huecos, ceros, cobertura, filtros encadenados y períodos, cambios rápidos, estados vacíos/carga/error, resumen y archivo descargado. Comparar valores con la consulta histórica equivalente y el CSV. Comprobar que cambiar la representación conserva el resultado y que no se exporta una selección anterior.

La integración con `HtmlRenderer` no sustituye la comprobación visual del gráfico Radzen ni de la descarga en navegador. Registrar el método de verificación y cualquier escenario no ejecutado.

### V3. Arquitectura y regresión

En la implementación, ejecutar después de los cambios de código:

```powershell
dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter 'FullyQualifiedName~Reportes|FullyQualifiedName~ProcedenciaHistorica'
dotnet test SolarUY.sln
```

Ejecutar con la conexión aislada para las integraciones que la requieren y registrar aprobadas, fallidas y omitidas por separado. Corregir el código ante fallos arquitectónicos sin alterar los guardarraíles de `CodeQuality.Tests`. Si falla una prueba existente, documentar su resultado real y la limitación; no relajar umbrales ni introducir cambios ajenos a REQ015 para ocultarlo.

Esta generación de documentos no requiere ejecutar pruebas de la aplicación: no modifica código ni acredita los resultados de una implementación futura.

## 6. Trazabilidad y cierre

| Criterio de aceptación de REQ015 | Decisiones | Verificación |
| --- | --- | --- |
| 1. Pantalla con barras y líneas Radzen | P1, P2 | V2 |
| 2. Generación por día, mes y año en kWh | A3, P1, P2 | V1.1, V2 |
| 3. Filtros por parque, nodo y fechas | A2, A3, I1, P1 | V1.2, V2 |
| 4. Actualización al cambiar filtros/período | A1, P3 | V1.7, V2 |
| 5. Consistencia con el histórico | D2, A3, A5, I1 | V1.1, V1.3, V1.6 |
| 6. CSV según filtros y períodos | A5, P4 | V1.6, V2 |
| 7. Mantenimientos realizados | A4, I1, P4 | V1.4, V2 |
| 8. Claridad y ausencia de datos | A3, P2, P3, P4 | V1.3, V1.4, V2 |
| 9. Roles autorizados | A2, P1 | V1.5, V2 |

### C1. Documentación de implementación

Derivar `tasks.md` únicamente de estas especificaciones, con todas las tareas pendientes. Para componentes ya existentes, verificar el resultado y corregir solo diferencias comprobadas antes de marcar la tarea como terminada.

Al cerrar la implementación, crear `implementations.md` en esta carpeta con cambios realizados, componentes reutilizados, archivos tocados, comandos y resultados de las pruebas, verificaciones visuales, omisiones y limitaciones. Si aparece un dato necesario que no esté resuelto por los requisitos ni por los contratos existentes, registrarlo como duda y tarea de clarificación antes de implementar la parte dependiente.
