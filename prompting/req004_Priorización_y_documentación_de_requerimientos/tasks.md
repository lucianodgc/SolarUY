# Tareas de implementación: Mantenimiento de paneles

Fuente de alcance técnico: `specs.md` de esta carpeta. Las referencias entre paréntesis identifican sus decisiones. Todas las tareas quedan pendientes; las que dependen de una aclaración se implementan después de resolverla y actualizar la documentación correspondiente.

## 1. Aclaraciones y dependencias

- [ ] 1.1 Confirmar y registrar el modelo, identificador, relación y disponibilidad de persistencia y consulta de `Panel`, coordinando la dependencia con `req003` sin duplicar su implementación. (C1)
- [ ] 1.2 Confirmar y registrar la fuente de técnicos asignables, su validación y el identificador estable que vincula al responsable con el usuario autenticado de Keycloak. (C2)
- [ ] 1.3 Definir y registrar el estado inicial, la obligatoriedad del responsable al crear, la representación del tipo de mantenimiento y la posibilidad de observaciones vacías. (C3)
- [ ] 1.4 Definir y registrar los estados de origen y roles autorizados para cancelar, la posibilidad de reactivar canceladas y cualquier transición adicional permitida. (C4)
- [ ] 1.5 Precisar y registrar los campos editables por el administrador, la reasignación y las condiciones de gestión de tareas canceladas. (C5)
- [ ] 1.6 Definir y registrar si la fecha programada es un día o un instante, la zona horaria y el límite exacto de vencimiento. (C6)
- [ ] 1.7 Actualizar los requisitos, `specs.md` y las tareas afectadas con las decisiones confirmadas, conservando la prohibición de modificar tareas completadas y sin ampliar el alcance por inferencia. (C1–C6)

## 2. Domain y pruebas de dominio

- [ ] 2.1 Crear `EstadoTareaMantenimiento` con `Pendiente`, `EnCurso`, `Completada` y `Cancelada`, sin añadir un estado persistido de vencimiento. (D1)
- [ ] 2.2 Crear `TareaMantenimiento` con identidad, relación con el `Panel` aprobado, tipo, fecha, responsable, estado y observaciones, con las representaciones confirmadas; agregar simultáneamente pruebas de creación e invariantes requeridas. (D1, V1; depende de C1, C2, C3 y C6)
- [ ] 2.3 Implementar las operaciones de asignación y las transiciones `Pendiente` a `EnCurso` y `EnCurso` a `Completada`, rechazando toda modificación de una completada; agregar simultáneamente pruebas de transiciones e inmutabilidad. (D2, V1)
- [ ] 2.4 Implementar exclusivamente la cancelación, transiciones adicionales y edición autorizadas tras C4/C5; agregar simultáneamente sus pruebas y comprobar que tampoco permiten modificar una completada. (D2, V1)
- [ ] 2.5 Implementar `EstaVencida` con referencia temporal explícita y la comparación aprobada, sin consultar el reloj desde Domain; agregar simultáneamente pruebas para pendientes y tareas en curso, exclusión de completadas y canceladas y límites temporales. (D3, V1; depende de C6)

## 3. Application

- [ ] 3.1 Definir los DTO de creación, asignación, cambio de estado, filtros y consulta con los datos acordados y vencimiento listo para mostrar; excluir identidad y roles del actor como datos de autorización de entrada. (A1)
- [ ] 3.2 Definir `IUsuarioActual` para exponer identidad autenticada y roles sin dependencias de ASP.NET o Blazor, usando la identidad estable acordada para comparar el responsable. (A2, A3; depende de C2)
- [ ] 3.3 Definir `ITareaMantenimientoRepository` para agregar, obtener, guardar y consultar tareas con alcance de acceso y filtros, sin exponer tipos de EF Core, SQL ni `IQueryable`. (A3, A4)
- [ ] 3.4 Reutilizar o definir los contratos mínimos de consulta y validación de paneles y técnicos según las fuentes confirmadas, sin agregar altas ni administración de usuarios. (A3; depende de C1 y C2)
- [ ] 3.5 Crear `ITareaMantenimientoService` y `TareaMantenimientoService` con inyección por constructor, operaciones asíncronas de persistencia y `CancellationToken`. (A1)
- [ ] 3.6 Implementar la creación exclusiva de administrador, validando panel existente y los datos requeridos, con errores controlados y sin crear paneles ni técnicos como efectos secundarios. (A1, A2)
- [ ] 3.7 Implementar la asignación exclusiva de administrador, validando al técnico y rechazando tareas completadas y las demás condiciones no permitidas por las decisiones aprobadas. (A1, A2)
- [ ] 3.8 Implementar inicio, finalización, cancelación y modificaciones aprobadas invocando el dominio, verificando el rol y la pertenencia de las tareas del técnico antes de guardar. (A1, A2; cancelación y edición dependen de C4/C5)
- [ ] 3.9 Implementar consultas y recuperación individual con alcance efectivo por usuario y filtros de estado, vencimiento, técnico y panel; obtener una referencia temporal coherente y devolver DTO con vencimiento calculado. (A2, A4)

## 4. Infrastructure

- [ ] 4.1 Agregar al `ApplicationDbContext` el conjunto y la configuración de `TareaMantenimiento`, con relación al panel existente y representación de campos y nulabilidad según las decisiones aprobadas. (I1)
- [ ] 4.2 Implementar `TareaMantenimientoRepository` con EF Core, aplicando en almacenamiento los filtros, el alcance de permisos y la misma comparación de vencimiento que Domain. (I1)
- [ ] 4.3 Implementar las escrituras condicionadas de forma atómica al estado validado y a que la tarea no esté completada; para operaciones del técnico, comprobar también que conserve la asignación. Devolver un error controlado si las condiciones cambiaron. (I2)
- [ ] 4.4 Reutilizar o implementar los adaptadores de consulta de paneles y de consulta y validación de técnicos correspondientes a las fuentes confirmadas, encapsulando cualquier integración externa en Infrastructure. (I1; depende de C1/C2)
- [ ] 4.5 Generar la migración incremental y actualizar el snapshot cuando la persistencia de Panel esté disponible, conservando el historial existente y la configuración externa de conexión, sin migraciones automáticas al arrancar. (I1)

## 5. Presentation y composición

- [ ] 5.1 Implementar `IUsuarioActual` desde el contexto autenticado del circuito de Blazor Server, exponiendo la identidad y roles confiables y sin depender exclusivamente del `HttpContext` inicial. (P3, A3)
- [ ] 5.2 Registrar el servicio, los contratos y adaptadores con inyección de dependencias, manteniendo el acceso a Infrastructure en el punto de composición del host. (P3)
- [ ] 5.3 Crear `MantenimientoPaneles` con Radzen para mostrar tareas, datos funcionales e indicador de vencimiento recibido de Application, con filtros de pendientes, en curso, vencidas, panel y técnico según el rol. (P2)
- [ ] 5.4 Crear `CrearTareaMantenimiento` para administrador, consultando paneles y técnicos a través de Application y capturando los datos de creación confirmados. (P1)
- [ ] 5.5 Conectar las acciones de asignar, iniciar, completar, cancelar y editar según lo aprobado a `ITareaMantenimientoService`; mostrar errores controlados, recargar tras guardar y permitir actualizar ante conflictos de escritura. (P1)
- [ ] 5.6 Proteger rutas, acciones y acceso al menú con `Administrador` y `Tecnico`; mostrar completadas sin acciones de modificación y mantener los cálculos y transiciones fuera de los componentes. (P1, P2)

## 6. Verificación

- [ ] 6.1 Ejecutar `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y confirmar que pasan las pruebas de creación, estados, inmutabilidad y vencimiento. (V1, V2)
- [ ] 6.2 Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` y corregir el código fuente ante incumplimientos, sin modificar los guardarraíles para hacerlos pasar. (V2)
- [ ] 6.3 Aplicar `dotnet ef database update --project SolarUY.Infrastructure --startup-project SolarUY.Presentation` contra MariaDB con configuración externa y comprobar persistencia y relación con panel existente. (V2)
- [ ] 6.4 Verificar manualmente creación, asignación, inicio, finalización, cancelación aprobada y filtros con usuarios de prueba de administrador y técnico. (V2)
- [ ] 6.5 Verificar manualmente la denegación de consultas y modificaciones de tareas ajenas para el técnico y de acceso para usuarios sin permiso, incluidos intentos por identificador directo. (V2, A2)
- [ ] 6.6 Comprobar la inmutabilidad de completadas y los límites de vencimiento acordados; reproducir una escritura desactualizada tras completar o reasignar desde otra sesión y confirmar que no altera la tarea. (V2, I2)

## 7. Cierre y documentación

- [ ] 7. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas. Incluir las decisiones aclaradas y las verificaciones no ejecutadas con su motivo. (V3)
