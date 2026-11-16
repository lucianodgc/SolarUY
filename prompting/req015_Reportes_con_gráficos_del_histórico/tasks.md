# Tareas: REQ015 — Reportes con gráficos del histórico y exportación

Fuente única de alcance técnico: [specs.md](specs.md). Cierre del 12/11/2026: las 47 tareas están completas; evidencia y límites en [implementations.md](implementations.md). La regresión final con MariaDB y simulador TCP terminó con 241 correctas, 0 fallidas y 4 omisiones explícitas. Se completó el recorrido en Chrome con los tres roles y la descarga real de CSV. Las referencias entre paréntesis identifican la decisión que respalda cada tarea.

## Preparación

- [x] 1. Contrastar los componentes existentes con el resultado esperado de REQ015 y delimitar las diferencias que requieren implementación. (C0, C1)
  - [x] 1.1. Registrar qué contratos, servicios, repositorios, componentes y pruebas de la tabla de C0 se reutilizan sin cambios y cuáles presentan diferencias concretas. (C0)
  - [x] 1.2. Si aparece un dato necesario no resuelto por la especificación ni por los contratos existentes, documentar la duda y crear una tarea de clarificación antes de implementar su parte dependiente. (C1)

## Domain

- [x] 2. Confirmar que el reporte se mantiene como proyección de lectura sobre el dominio existente. (D1, D2)
  - [x] 2.1. Verificar que el alcance no requiere entidades, atributos, estados, relaciones, comportamiento de dominio ni migraciones adicionales. (D1)
  - [x] 2.2. Comprobar que la consulta reutilizada respeta el nodo histórico, excluye lecturas de procedencia desconocida de agregados por nodo/parque y conserva el diagnóstico sin atribuirlo al objetivo seleccionado. (D2)

## Application

- [x] 3. Verificar y, cuando corresponda, ajustar el módulo existente `SolarUY.Application.Reportes`. (A1–A5)
  - [x] 3.1. Conservar `IReporteHistoricoService` y sus tres operaciones con cancelación, las dependencias por constructor y los contratos independientes de UI y persistencia. (A1)
  - [x] 3.2. Comprobar que `ResultadoReporteDto` conserva filtro, identificación, generación, mantenimientos y gráfico mediante colecciones de solo lectura para consulta y exportación. (A1)
  - [x] 3.3. Aplicar autenticación, identificador válido y roles `Administrador`, `Tecnico` o `Consulta` en opciones, consulta y exportación, sin ampliar permisos de gestión de mantenimiento. (A2)
  - [x] 3.4. Validar identificadores, período, existencia y pertenencia del nodo al parque; conservar parque completo cuando no se selecciona nodo y errores diferenciados. (A2)
  - [x] 3.5. Delegar la consulta y validación temporal en `IHistoricoGeneracionService`, conservando Diario/Mensual/Anual/Rango, zona Uruguay, límites exclusivos e intervalos de A3. (A3)
  - [x] 3.6. Preservar energía, unidad kWh, precisión, cobertura, orden, ceros y ausencias del histórico; mantener `GraficoReporteDto` con segmentos separados en los huecos. (A3)
  - [x] 3.7. Coordinar el resumen mediante `ListarRealizadosParaReporteAsync` con parque/nodo validado y límites UTC del resultado histórico, incluso sin lecturas. (A4)
  - [x] 3.8. Conservar tareas Completadas con fecha de realización dentro del período y los campos existentes del resumen, sin sustituir esa fecha por la programada. (A4)
  - [x] 3.9. Serializar el resultado mostrado mediante `ReporteCsv`, volviendo a autorizar y validando su coherencia sin reconsultar lecturas. (A5)
  - [x] 3.10. Mantener las 24 columnas, tipos de registro, metadatos, UTF-8 sin BOM, separador, CRLF, escape, fechas y decimales especificados, incluidos intervalos vacíos, ceros y avisos de procedencia. (A5)

## Infrastructure

- [x] 4. Verificar y corregir únicamente diferencias de las consultas de lectura existentes. (I1)
  - [x] 4.1. Reutilizar `HistoricoGeneracionRepository` a través del servicio histórico, conservando procedencia y datos sin otro cálculo ni repositorio de reportes. (I1, D2)
  - [x] 4.2. Comprobar en `TareaMantenimientoRepository` los filtros de parque/nodo, estado Completada, fecha no nula y límites `[InicioUtc, FinUtc)`, mediante relaciones persistidas tarea/panel/nodo/parque. (I1, A4)
  - [x] 4.3. Conservar contexto por operación, lectura sin seguimiento, proyección de campos, orden estable y cancelación, sin consultas por tarea ni cambios de gestión o infraestructura. (I1)

## Presentation

- [x] 5. Verificar y, cuando corresponda, ajustar la página `/reportes` y su descarga. (P1–P4)
  - [x] 5.1. Mantener `ReportesHistorico.razor`, autorización de los tres roles, acceso del menú y registro del servicio en `Program.cs`, sin acceso a repositorios desde componentes. (P1)
  - [x] 5.2. Conectar filtros de parque, nodo opcional, período y fechas a las opciones del servicio; limpiar el nodo al cambiar de parque e indicar zona horaria y fin exclusivo. (P1)
  - [x] 5.3. Reutilizar gráficos Radzen de barras/líneas con selector de representación, ejes temporales y energía en kWh, consumiendo los puntos y segmentos de Application. (P2)
  - [x] 5.4. Conservar total, detalle completo paginado, ceros, huecos, cobertura y avisos mediante `ResultadoHistorico.razor`, sin recalcular energía en Blazor. (P2, A3)
  - [x] 5.5. Actualizar generación y mantenimiento juntos al cambiar filtros/período, invalidando el resultado anterior y descartando respuestas tardías aun cuando ignoren la cancelación. (P3)
  - [x] 5.6. Mantener cancelación al desechar la página y estados de carga, entrada inválida, acceso denegado, inexistencia, ausencia de datos y errores de consulta/descarga. (P3)
  - [x] 5.7. Mostrar los campos del mantenimiento realizado, fechas de Uruguay y estado vacío independiente de la generación, sin acciones de gestión. (P4, A4)
  - [x] 5.8. Descargar el CSV del resultado vigente mediante `reportes.js`, deshabilitando exportación sin resultado vigente o durante carga/descarga y descartando una descarga si cambian sus filtros. (P3, P4)

## Verificación

- [x] 6. Evaluar las integraciones existentes y añadir únicamente los escenarios faltantes descritos en V1. (V1)
  - [x] 6.1. Preparar MariaDB 11.8 aislada con `SOLARUY_HISTORICO_TEST_CONNECTION`, prefijo de base `solaruy_req007_test` y colección existente sin paralelismo; registrar si la conexión no está disponible. (V1)
  - [x] 6.2. Cubrir períodos, objetivos y equivalencia exacta con el histórico, límites temporales y rechazo de filtros inválidos o ajenos. (V1.1, V1.2)
  - [x] 6.3. Cubrir ceros, ausencias, cobertura, segmentos, procedencia desconocida y traslado de panel sin reatribución de lecturas. (V1.3)
  - [x] 6.4. Cubrir mantenimiento por realización, estados/fechas excluidos, límites y objetivos, con generación ausente o ambos conjuntos vacíos. (V1.4)
  - [x] 6.5. Cubrir opciones, lectura y exportación para los tres roles, denegaciones y conservación de los permisos de gestión. (V1.5)
  - [x] 6.6. Cubrir formato y equivalencia completa del CSV, avisos, exportación sin reconsulta tras agregar una lectura y ausencia de escrituras de consulta/exportación. (V1.6)
  - [x] 6.7. Cubrir respuestas fuera de orden, invalidación de resultados y cancelación mediante la página real y su integración existente. (V1.7)
- [x] 7. Completar el recorrido visual de `/reportes` con datos conocidos y los tres roles, registrando evidencia y escenarios pendientes. (V2)
  - [x] 7.1. Verificar barras/líneas, ejes/kWh, períodos, valores, ceros, huecos, cobertura y avisos; comprobar que cambiar de representación conserva el resultado. (V2)
  - [x] 7.2. Verificar filtros encadenados, cambios rápidos, carga/errores, estados vacíos y resumen de mantenimiento con el período seleccionado. (V2)
  - [x] 7.3. Descargar y comparar el CSV con el resultado visible y la consulta histórica equivalente, comprobando que no se exporta una selección anterior. (V2)
- [x] 8. Ejecutar arquitectura, integraciones y regresión, registrando resultados reales sin alterar guardarraíles ni umbrales. (V3)
  - [x] 8.1. Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` tras los cambios de código y corregir el código ante incumplimientos. (V3)
  - [x] 8.2. Ejecutar `dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter 'FullyQualifiedName~Reportes|FullyQualifiedName~ProcedenciaHistorica'` con la conexión aislada; registrar aprobadas, fallidas y omitidas. (V3)
  - [x] 8.3. Ejecutar `dotnet test SolarUY.sln` y registrar resultados, fallos previos y verificaciones no realizadas sin declarar aprobadas las omitidas. (V3)

## Cierre / Documentación

- [x] 9. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
