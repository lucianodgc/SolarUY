# Tareas: Histórico de generación por día, mes y año

Fuente técnica de alcance: `specs.md` de esta carpeta. Las referencias entre paréntesis corresponden a sus secciones. Estado al 22/10/2026: ampliación de recolección implementada y verificada; funciona mientras la aplicación está en ejecución. La verificación de volumen (6.8) se reabre por latencia superior al umbral; evidencia y resultados en `implementations.md`.

## 1. Aclaraciones y dependencias

Q1–Q8 fueron confirmadas por el usuario y registradas en `specs.md` antes de cerrar sus componentes dependientes.

- [x] 1.1 Acordar la regla de conversión de potencia a energía: significado de muestras, frecuencia, integración, límites, huecos, duplicados y precisión/redondeo. Registrar fórmula y ejemplos esperados. (Q1)
- [x] 1.2 Confirmar los valores de `LecturaPanel.EstadoSalud`, validaciones de potencia y precisión persistida, conservando los atributos autorizados del modelo. (Q2, D1, I1)
- [x] 1.3 Definir zona horaria, extremos de fechas, granularidad de cada vista y comportamiento de comparación entre rangos de distinta duración. (Q3)
- [x] 1.4 Definir magnitud, unidad, granularidad, empates y referencia temporal de máximos y mínimos. (Q4)
- [x] 1.5 Definir ausencia de lecturas, cobertura parcial, generación medida igual a cero y presentación de intervalos vacíos. (Q5)
- [x] 1.6 Confirmar roles y alcance de lectura por usuario, incluyendo opciones de selección y acceso directo por identificador. (Q6)
- [x] 1.7 Acordar volumen/distribución, rangos/granularidades, concurrencia, entorno y umbrales medibles de latencia y memoria. (Q7)
- [x] 1.8 Confirmar fuente y responsabilidad de carga de lecturas persistidas del inventario. Registrar la dependencia operativa sin implementar un recolector o enlazar automáticamente el simulador. (C0, Q8, I3)

## 2. Domain

Dependencias: resolver 1.1 y 1.2 antes de cerrar el modelo de lectura. Reutilizar `ParqueSolar`, `Nodo` y `Panel` existentes. (C0, D1)

- [x] 2.1 Incorporar `LecturaPanel` y la definición de estado acordada, con `Id` generado en constructor, `PanelId`/`Panel`, `FechaHora` UTC, `PotenciaGeneradaW` y `EstadoSalud`, sin atributos adicionales. (D1)
- [x] 2.2 Implementar validaciones del constructor y propiedades con `private set`, junto con pruebas xUnit de identidad, asociación, valores conservados, UTC y rechazos definidos en Q2. Mantener Domain independiente de frameworks. (D1, D2)

## 3. Application

Dependencias: aclarar 1.1–1.6 para cerrar semántica de contratos y permisos. (A1, A2)

- [x] 3.1 Definir `FiltroHistoricoDto`, `ResultadoHistoricoDto`, `ComparacionHistoricoDto` y `OpcionHistoricoDto` con objetivo, períodos, unidades, agregados, extremos y disponibilidad según las aclaraciones. (A1)
- [x] 3.2 Crear `IHistoricoGeneracionRepository` con consultas agregadas, comprobación de existencia y opciones bajo alcance autorizado, sin exponer EF Core, `IQueryable` ni colecciones completas de lecturas. (A1)
- [x] 3.3 Crear `IHistoricoGeneracionService`/`HistoricoGeneracionService` con `ConsultarAsync`, `CompararAsync` y `ListarOpcionesAsync`, inyección por constructor y `CancellationToken`. (A1)
- [x] 3.4 Aplicar identidad, permisos y alcance de Q6 a todas las operaciones, antes de consultar datos, incluyendo opciones y acceso directo por identificador. (A2)
- [x] 3.5 Validar objetivo y períodos, resolver límites temporales y conversión UTC según Q3, y distinguir errores controlados de entrada, autorización e inexistencia. (A2)
- [x] 3.6 Orquestar consulta histórica y comparación del mismo objetivo autorizado, entregando agregados y disponibilidad listos para mostrar, sin cargar ni calcular sobre muestras en memoria. (A1, A2)
- [x] 3.7 Implementar opciones del inventario persistido y comprobar que los contratos puedan invocarse sin depender de Blazor o de la sesión visual, conservando la autorización del usuario. (A1–A3)

## 4. Infrastructure

Dependencias: entidad de la etapa 2, puerto de 3.2 y aclaraciones Q1–Q6. (I1, I2)

- [x] 4.1 Agregar `DbSet<LecturaPanel>` y `LecturaPanelConfiguration` para `LecturasPanel`, clave, FK a `Paneles`, relación restrictiva, fecha UTC, estado y precisión acordados. (I1)
- [x] 4.2 Incorporar el índice no único `(PanelId, FechaHora, PotenciaGeneradaW)` y generar `AddLecturaPanel` con su snapshot, conservando las migraciones históricas. (I1)
- [x] 4.3 Crear `HistoricoGeneracionRepository` con `IDbContextFactory<ApplicationDbContext>`, contexto por operación, lectura sin seguimiento, cancelación y parámetros. Registrar su interfaz en `AddSolarInfrastructure`. (I2)
- [x] 4.4 Implementar comprobación de objetivo y opciones autorizadas sobre el inventario persistido, sin utilizar los identificadores del simulador. (A1, A2, I2)
- [x] 4.5 Implementar filtros SQL por panel/nodo/parque, alcance autorizado y límites UTC mediante las relaciones existentes, sin cargar listas completas ni excluir históricos por estado actual del panel. (I2)
- [x] 4.6 Implementar en base de datos la conversión de potencia a energía, agrupaciones y totales según Q1/Q3, procesando también en el motor las muestras auxiliares acotadas que requiera la fórmula. Usar EF Core/Pomelo o SQL parametrizado encapsulado cuando la traducción no sea posible. (I2)
- [x] 4.7 Implementar máximos/mínimos sobre la magnitud y granularidad acordadas, sumando previamente los aportes del mismo intervalo cuando se trate de generación conjunta, y aplicar Q4/Q5 a empates y disponibilidad. (I2)
- [x] 4.8 Entregar únicamente resultados agregados para consulta/comparación, sin evaluación de muestras en cliente, consultas por cada panel ni operaciones de escritura. (I2, I3)

## 5. Presentation y composición

Dependencias: servicio y repositorio de etapas 3–4 y política de Q6. (P1, P2)

- [x] 5.1 Registrar `IHistoricoGeneracionService` en la composición existente del host y crear `HistoricoGeneracion.razor` en `/historico-generacion`, con página y menú protegidos según Q6. (P1)
- [x] 5.2 Implementar selección de parque/nodo/panel mediante `ListarOpcionesAsync`, controles diarios/mensuales/anuales y rangos de consulta/comparación según Q3. (P1)
- [x] 5.3 Mostrar total de energía, serie en tabla Radzen, períodos comparados y extremos con sus unidades, referencias temporales, límites y zona horaria, usando los DTO ya calculados. (P2)
- [x] 5.4 Mostrar disponibilidad según Q5 y estados de carga, validación, acceso denegado, inexistencia y error; impedir consultas repetidas mientras haya una operación en curso. Mantener los cálculos y las decisiones de negocio en Application/Infrastructure. (P2)

## 6. Verificación

Dependencias: implementación de etapas 2–5; Q1–Q6 para fijar resultados, Q7 para aceptar rendimiento y Q8 para registrar disponibilidad operativa. (V1–V4)

- [x] 6.1 Ejecutar `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj`; corregir el código ante fallos sin modificar los guardarraíles. (V1)
- [x] 6.2 Preparar la integración con MariaDB 11.8 en una base aislada configurada externamente, aplicar la migración y cargar datos conocidos de dos parques con nodos/paneles y lecturas en distintos días, meses y años. Fijar resultados independientes de la consulta implementada. (V2)
- [x] 6.3 Incorporar pruebas en `SolarUY.Integration.Tests` de consultas diarias/mensuales/anuales por los tres objetivos, aislamiento, suma jerárquica y comparación entre rangos. (V2)
- [x] 6.4 Incorporar pruebas de conversión W a kWh, límites temporales y cambios de mes/año, muestras auxiliares, huecos y duplicados conforme a Q1/Q3. (V2)
- [x] 6.5 Incorporar pruebas de máximos/mínimos, empates, falta de datos, cobertura parcial y cero medido conforme a Q4/Q5. (V2)
- [x] 6.6 Incorporar pruebas de rangos invertidos, identificadores inválidos/inexistentes y permisos para consultas, comparación y opciones según Q6. (V2)
- [x] 6.7 Verificar mediante integración que consultar conserva las lecturas y que la nueva FK es compatible con la baja de un parque temporal de req006, sin afectar las lecturas del otro parque. (V2)
- [ ] 6.8 Ejecutar el escenario de volumen/concurrencia acordado, medir latencia y memoria e inspeccionar SQL/planes para verificar agregación en servidor, filtrado e índices; registrar resultados frente a Q7. **Pendiente: la comprobación actual de parque/mes alcanzó 5,487 s frente al límite de 5 s.** (V3)
- [x] 6.9 Recorrer la página con los roles acordados y comprobar selecciones, períodos, comparación, extremos, unidades/zona, mensajes y denegación de acceso. (V4)
- [x] 6.10 Ejecutar `dotnet test SolarUY.sln` con la configuración de integración necesaria y registrar resultados de pruebas existentes/nuevas y cualquier comprobación no ejecutada. (V4)

## 7. Ampliación: recolección periódica

- [x] 7.1 Registrar la ampliación de Q8 y el criterio de recolección del inventario en requisitos y especificación. (R1)
- [x] 7.2 Incorporar contratos de medición del inventario, sesión exclusiva y servicio interno de recolección en Application. (R1)
- [x] 7.3 Implementar validación del lote y creación de lecturas reutilizando Domain, sin generar muestras anteriores a la activación. (R1)
- [x] 7.4 Implementar sesión MariaDB con bloqueo por base, liberación garantizada, selección de paneles pendientes por minuto y guardado atómico. (R1)
- [x] 7.5 Ampliar el simulador para medir los paneles persistidos con sus identificadores, potencia nominal y estado. (R1)
- [x] 7.6 Registrar el worker, el ámbito por ciclo, temporizador, configuración validada, cancelación y continuidad ante errores. (R1)
- [x] 7.7 Incorporar pruebas de integración de mediciones, estados, idempotencia, inventario dinámico y consumo por el histórico. (R1)
- [x] 7.8 Verificar exclusión entre instancias y liberación de sesión ante error/cancelación, incluyendo el worker real. (R1)
- [x] 7.9 Ejecutar los guardarraíles y la solución con la integración configurada; registrar resultados y ejecutar un ciclo real en la base habitual. (R1)
- [x] 7.10 Esperar el arranque completo de la web antes de recolectar y comprobar con Kestrel y un puerto ocupado que el inicio fallido no guarda lecturas ni produce fallos secundarios al cerrar el worker. Detener la instancia local de verificación para liberar el puerto habitual. (R1)

## 8. Cierre y documentación

- [x] 8. Actualizar implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas, incluida la recolección periódica. (C1, R1)
