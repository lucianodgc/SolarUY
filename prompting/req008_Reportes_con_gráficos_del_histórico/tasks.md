# Tareas: Reportes con gráficos del histórico y exportación

Fuente única de alcance técnico: `specs.md` de esta carpeta. Las referencias entre paréntesis identifican sus secciones. Las 32 tareas se completaron el 22/10/2026; Q1 y Q2 fueron confirmadas por el usuario. Evidencia: `implementations.md`. La solución completa conserva el fallo de rendimiento previamente pendiente en req007, tarea 6.8; no se declara aprobada esa comprobación.

## 1. Aclaraciones

- [x] 1.1 Confirmar qué mantenimientos se consideran realizados, estado y fecha usados en el período, campos del resumen y tratamiento de registros sin fecha de realización. Registrar la respuesta en `specs.md` antes de implementar la consulta y fijar sus resultados esperados. (Q1, C1)
- [x] 1.2 Confirmar si el CSV incluye mantenimiento, estructura del archivo, columnas y orden, separador, codificación, fechas, precisión y representación de ausencia de datos. Registrar el contrato en `specs.md` antes de implementar la exportación y sus verificaciones. (Q2, C1)

## 2. Application

Domain no requiere modificaciones ni tareas de implementación: se reutilizan sus entidades y estados. No crear migraciones o entidades para representar reportes. (D1)

- [x] 2.1 Crear `FiltroReporteDto`, `ResultadoReporteDto` y `ConsultaMantenimientosReporteDto`, reutilizando los contratos de req007 y dejando los campos de mantenimiento sujetos a 1.1. (A1, A3)
- [x] 2.2 Definir `IReporteHistoricoService`/`ReporteHistoricoService` con opciones, consulta y exportación asíncronas, cancelación y dependencias por constructor; sin referencias a Radzen, EF Core o HTTP. (A1)
- [x] 2.3 Aplicar autenticación y roles Administrador/Tecnico/Consulta a opciones, consulta y exportación; ofrecer parques/nodos desde el histórico y validar existencia y pertenencia del nodo al parque. (A2)
- [x] 2.4 Delegar generación y validación temporal en `IHistoricoGeneracionService`, conservando límites, zona, intervalos, energía, precisión y cobertura, sin duplicar cálculos o conversión de fechas. (A2, A3)
- [x] 2.5 Tras resolver 1.1, definir `MantenimientoRealizadoReporteDto` y ampliar `ITareaMantenimientoRepository` con `ListarRealizadosParaReporteAsync`, usando los filtros y campos acordados. (A1, A3)
- [x] 2.6 Integrar los mantenimientos del mismo parque/nodo y límites UTC del resultado histórico en `ResultadoReporteDto`, también cuando no haya lecturas de energía. (A3)
- [x] 2.7 Tras resolver 1.2, definir `ArchivoReporteCsvDto` e implementar la serialización con los campos, precisión, ausencia de datos y escape acordados; incluir todos los intervalos del resultado. (A1, A4)
- [x] 2.8 Hacer que la exportación vuelva a autorizar y validar el resultado generado en el servidor, usando los mismos valores mostrados sin reconsultar lecturas ni aceptar filas del navegador. (A2, A4)

## 3. Infrastructure

Dependencias: contrato de 2.5 y aclaración 1.1. La consulta de generación y la infraestructura de recolección conservan su implementación existente. (C0, I2)

- [x] 3.1 Implementar `ListarRealizadosParaReporteAsync` en `TareaMantenimientoRepository`, con contexto por operación, cancelación, lectura sin seguimiento y proyección de campos del resumen. (I1)
- [x] 3.2 Aplicar en MariaDB los filtros acordados de estado/fecha, parque y nodo opcional mediante las relaciones existentes, con orden estable; evitar cargar registros ajenos al período u objetivo y consultas por cada panel. (I1)
- [x] 3.3 Conservar las restricciones por responsable y las validaciones de las operaciones existentes de mantenimiento, sin cambios de esquema, escrituras ni infraestructura adicional de exportación. (I1, I2)

## 4. Presentation y composición

Dependencias: contratos y servicio de la etapa 2; consulta de la etapa 3 para completar el resumen. La descarga depende de 1.2 y de 2.7–2.8. (P1–P4)

- [x] 4.1 Registrar `IReporteHistoricoService` en `Program.cs` siguiendo el ámbito e identidad del circuito del histórico; crear `ReportesHistorico.razor` en `/reportes` y su acceso al menú para los tres roles. (P1)
- [x] 4.2 Implementar filtros por parque, nodo opcional, período Diario/Mensual/Anual/Rango y fechas; obtener opciones del servicio y limpiar nodos incompatibles al cambiar de parque. (P1)
- [x] 4.3 Actualizar automáticamente el reporte al cambiar filtros válidos, cancelando consultas anteriores y descartando respuestas tardías; cancelar operaciones al desechar la página. (P3)
- [x] 4.4 Incorporar gráficos de barras y líneas con `RadzenChart` y selector de representación, alimentados por el mismo resultado y con etiquetas temporales, objetivo, zona y unidad kWh. (P2)
- [x] 4.5 Verificar el soporte de datos ausentes de Radzen.Blazor 6.0.0 y representarlos sin ceros ficticios ni líneas atravesando huecos; conservar intervalos, cero medido y cobertura parcial. Preparar adaptaciones de DTO en Application si el componente las necesita. (A3, P2)
- [x] 4.6 Mostrar el resumen de mantenimiento con los campos de 1.1 y fechas con zona explícita, reutilizando conceptos del módulo existente y sin acciones de gestión. (P4)
- [x] 4.7 Implementar carga, validación, acceso denegado, inexistencia, ausencia de energía/mantenimiento y errores; impedir que resultados previos o su exportación se presenten bajo filtros nuevos. (P2, P3)
- [x] 4.8 Descargar el CSV del resultado vigente mediante el caso de uso y la interoperabilidad Blazor/JavaScript, liberando recursos temporales y sin paquetes nuevos ni archivos permanentes en servidor. (A4, P4)

## 5. Verificación

Dependencias: implementación de etapas 2–4 y respuestas a Q1/Q2 para los casos correspondientes. Las verificaciones de integración usan una base aislada y servicios/repositorios reales, sin pruebas unitarias de las capas externas ni cambios de guardarraíles. (V1–V3)

- [x] 5.1 Preparar datos conocidos de dos parques y sus nodos, lecturas y mantenimientos en MariaDB 11.8 aislada; reutilizar la conexión externa y la serialización de preparación de req007. (V1)
- [x] 5.2 Agregar integraciones de consultas diarias, mensuales, anuales y por rango para parque/nodo, comparando la energía, serie, intervalos y cobertura con el servicio histórico existente. Incluir cero medido, huecos y cobertura parcial. (V1)
- [x] 5.3 Verificar aislamiento por parque/nodo, pertenencia inválida, inexistencia, fechas inválidas, límites Uruguay/UTC y cancelación. (V1)
- [x] 5.4 Verificar los mantenimientos según 1.1, sus límites temporales, estados excluidos y ausencia de datos; incluir un período sin lecturas pero con mantenimiento y registros ajenos al objetivo. (V1)
- [x] 5.5 Verificar consulta/exportación de todos los parques por los tres roles, denegación a identidades no autorizadas y conservación de los permisos actuales de gestión de mantenimiento. (V1)
- [x] 5.6 Verificar el CSV según 1.2 con precisión, metadatos, intervalos sin datos, caracteres especiales y escape; agregar una lectura después de consultar y comprobar que el archivo conserva los valores del resultado mostrado. (V1)
- [x] 5.7 Comprobar que consultar y exportar conserva las lecturas y tareas persistidas de la base aislada. (V1)
- [x] 5.8 Recorrer la pantalla con los tres roles para verificar barras/líneas, huecos, cero, cobertura, filtros, períodos, mantenimiento, estados y descarga; contrastar gráfico y CSV con los mismos resultados del histórico. Registrar método y casos no ejecutados. (V2)
- [x] 5.9 Comprobar cambios rápidos de filtros y respuestas fuera de orden, exportación deshabilitada cuando el resultado no está vigente y cambio de representación sin variar los datos. (V2)
- [x] 5.10 Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj`, las nuevas integraciones y `dotnet test SolarUY.sln` con MariaDB aislada configurada. Registrar resultados y omisiones, incluido el estado del fallo de volumen previamente documentado en req007, sin cambiar su umbral. (V3)

## 6. Cierre y documentación

- [x] 6. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas. (C1)
