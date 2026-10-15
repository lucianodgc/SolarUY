# Especificaciones técnicas: Mantenimiento de paneles

## Contexto, alcance y trazabilidad

Fuente funcional: `requierements.md` de esta carpeta; se conserva su nombre actual. Este documento define la implementación posterior, sin dar por resueltas las aclaraciones pendientes.

El alcance comprende crear tareas asociadas a paneles existentes, asignarlas a técnicos, gestionar sus estados y consultar tareas por estado, vencimiento, técnico y panel. No incluye alta de paneles, administración de usuarios, eliminación de tareas, notificaciones, mantenimiento automático ni historial de auditoría.

Se mantienen .NET 8, Blazor Server con Radzen, Keycloak mediante OpenID Connect, MariaDB 11.8 LTS, EF Core 8 y Pomelo 8.0.3. No se incorporan bibliotecas, proveedores ni MediatR. Domain permanece independiente; Application coordina los casos de uso sin conocer EF Core ni Blazor; Infrastructure implementa persistencia e integraciones; Presentation invoca Application.

| Referencia | Requisito de origen | Diseño que lo cubre |
| --- | --- | --- |
| R1 | Registro y datos de una tarea | D1, D2, A1, I1, P1 |
| R2 | Creación por administrador y asignación a técnicos | A1, A2, A3, P1 |
| R3 | Gestión de estados | D2, A1, I2, P1 |
| R4 | Consultas y definición de vencimiento | D3, A4, I1, P2 |
| R5 | Permisos por rol y tareas propias | A2, A3, I1, P1, P2 |
| R6 | Panel existente y datos requeridos | D1, A1, I1 |
| R7 | Inmutabilidad de tareas completadas | D2, A1, I2, P1 |
| R8 | Pruebas de dominio exigidas | V1 |

## Aclaraciones y dependencias pendientes

Las siguientes decisiones deben registrarse en los requisitos y reflejarse en este documento antes de implementar las partes afectadas. No se elige una solución funcional o una integración externa sin esa definición.

| Referencia | Duda o dependencia | Parte afectada |
| --- | --- | --- |
| C1 | No existe una entidad `Panel` en el código. `req003_infraestructura_base_de_datos/specs.md` también deja pendiente su definición. Confirmar el modelo, identificador, relación de dominio y disponibilidad de su persistencia y consulta. Reutilizar ese trabajo cuando esté disponible; este requerimiento no define el alta ni los atributos de un panel. | Relación de dominio, validación de existencia, selector y clave foránea. |
| C2 | Keycloak ya define el rol técnico como `Tecnico`, pero no existe un directorio de técnicos en Application. Confirmar cómo obtener y validar técnicos asignables y qué identificador estable los vincula con el usuario autenticado. No asumir acceso a la API administrativa de Keycloak, credenciales nuevas ni una tabla local de usuarios. | Responsable, asignación, consulta de tareas propias e integración con la fuente de técnicos. |
| C3 | Confirmar el estado inicial, si se puede crear una tarea sin responsable y cómo se proporciona el tipo de mantenimiento: texto o catálogo existente. Confirmar si las observaciones pueden quedar vacías. No inventar tipos, límites de texto ni valores predeterminados. | Construcción, DTO de creación, formulario y restricciones de persistencia. |
| C4 | Definir desde qué estados y por qué roles se puede cancelar una tarea, si una cancelada puede reactivarse y qué transiciones adicionales, si alguna, están permitidas. La prohibición de modificar una completada ya está definida y no queda pendiente. | Métodos de transición, autorización, acciones visibles y pruebas. |
| C5 | Precisar qué significa que el administrador pueda «gestionar tareas mientras no estén completadas»: campos editables, reasignación y condiciones sobre tareas canceladas. No añadir edición general ni cambios de panel o fecha por inferencia. | Operaciones adicionales de modificación, interfaz y pruebas de inmutabilidad. |
| C6 | Definir si la fecha programada representa un día o un instante, la zona horaria y el límite exacto de vencimiento, incluidos igualdad con la referencia y fin de día. No asumir UTC, hora local del servidor ni rechazo de fechas pasadas. | Tipo de fecha, comparación de vencimiento, filtros, persistencia y presentación. |

Las operaciones dependientes de C1 y C2 no pueden entregarse como funcionales mediante paneles o técnicos ficticios. La integración se implementará contra el modelo y la fuente confirmados. C3 a C6 condicionan únicamente las decisiones que describen; los comportamientos explícitos de los requisitos se mantienen.

## 1. Domain

### D1. Modelo de tarea

Crear `TareaMantenimiento` en `SolarUY.Domain`, con identidad propia y los datos funcionales: panel asociado, tipo de mantenimiento, fecha programada, responsable, estado y observaciones. La representación de los campos pendientes se cerrará con C1, C2, C3 y C6 antes de definir firmas definitivas y tipos de persistencia.

La asociación con el panel será una relación de objetos con la entidad `Panel` aprobada, no una entidad duplicada ni una clave primitiva que sustituya toda la relación semántica. La comprobación de existencia en almacenamiento corresponde a Application. No se modifica `ParqueSolar` ni se diseña el modelo de Nodo o Panel para completar la estructura de capas.

El responsable representa al técnico asignado mediante la identidad estable acordada en C2. No se incorporan credenciales ni dependencias de Keycloak al dominio, ni se crea una entidad de usuario sin una definición aprobada.

Definir `EstadoTareaMantenimiento` con los únicos valores funcionales `Pendiente`, `EnCurso`, `Completada` y `Cancelada`. `Vencida` no es un estado persistido.

La creación exige panel asociado, fecha programada y tipo definido. Las condiciones del responsable, observaciones y estado inicial se ajustarán a C3. Las invariantes se validan en la entidad y no mediante anotaciones de EF Core o lógica de componentes Blazor.

### D2. Comportamiento e inmutabilidad

La entidad expone operaciones de negocio para cambiar su estado y, tras C5, modificar exclusivamente los datos autorizados. El estado y los datos modificables no se exponen mediante setters públicos que permitan omitir las invariantes.

Las transiciones confirmadas son:

| Estado de origen | Operación | Estado de destino |
| --- | --- | --- |
| `Pendiente` | Iniciar mantenimiento | `EnCurso` |
| `EnCurso` | Completar mantenimiento | `Completada` |

Completar conserva la tarea y registra el estado `Completada`; no exige una fecha de finalización, un informe adicional ni observaciones obligatorias porque no están requeridos.

Toda operación de modificación sobre una tarea `Completada` debe rechazarse sin alterar sus datos, incluidas asignación, cambios de estado, cancelación y futuras modificaciones aprobadas en C5. Las transiciones no definidas requieren C4; no se habilitan retornos o cierres directos por conveniencia técnica. Los permisos del usuario se aplican en Application, sin introducir claims o frameworks de autorización en Domain.

### D3. Vencimiento

La entidad expone `EstaVencida` recibiendo la referencia temporal como argumento, con el tipo acordado en C6. Una tarea está vencida cuando su fecha programada ya pasó y su estado no es `Completada` ni `Cancelada`. Por ello, tanto una tarea pendiente como una en curso pueden estar vencidas.

No se persiste un indicador de vencimiento ni se ejecutan procesos para cambiar el estado al pasar la fecha. El dominio no consulta el reloj del servidor; recibir la referencia permite verificar el comportamiento de forma determinista. La comparación exacta se cierra con C6 y debe coincidir con la consulta de Infrastructure.

## 2. Application

### A1. Contratos y casos de uso

Crear `ITareaMantenimientoService` y `TareaMantenimientoService` para las operaciones requeridas: crear, asignar, iniciar, completar, cancelar según C4 y consultar tareas. Los métodos serán asíncronos cuando accedan a persistencia, aceptarán `CancellationToken` e inyectarán sus dependencias por constructor. Las operaciones adicionales de edición dependen de C5.

Definir DTO de creación, asignación, cambio de estado, filtros y resultados de consulta con los datos necesarios para estos flujos. El DTO de consulta entrega los datos listos para mostrar, incluido el vencimiento calculado. Los DTO de entrada no aceptan identidad ni roles del actor como prueba de autorización.

Cada modificación carga la tarea existente, comprueba permisos, invoca su comportamiento de dominio y guarda los cambios. La creación comprueba que el panel exista y la asignación valida al técnico mediante la fuente confirmada. Un panel inexistente, técnico inválido, tarea inexistente, transición inválida, falta de permisos o intento de modificar una completada producen un error controlado y no guardan cambios.

No se crea automáticamente un panel o un usuario como efecto secundario. Las validaciones funcionales definitivas de los DTO siguen C3 a C6; no se añaden restricciones de fecha o texto sin respaldo.

### A2. Identidad y permisos efectivos

Definir `IUsuarioActual` en Application como abstracción de la identidad autenticada y sus roles, sin tipos de ASP.NET o Blazor. Su implementación en el host obtiene la sesión autenticada de Blazor Server, según A3 y P3. El técnico destinatario de una asignación es un dato de la operación; el usuario que la ejecuta siempre procede del contexto confiable.

Aplicar los siguientes permisos dentro de los casos de uso, además de las restricciones de interfaz:

| Operación | Administrador | Tecnico |
| --- | --- | --- |
| Crear tarea | Permitida | No permitida |
| Asignar a un técnico | Permitida mientras la tarea no esté completada; condiciones sobre canceladas según C4/C5 | No permitida |
| Consultar tareas | Tareas de mantenimiento | Solo tareas asignadas al usuario actual |
| Iniciar o completar | Gestión de tareas no completadas según las transiciones definidas | Solo sus tareas asignadas y según las transiciones definidas |
| Cancelar y otras modificaciones | Según C4/C5 y sin modificar completadas | Solo acciones confirmadas en C4/C5 sobre tareas propias |

El nombre técnico del rol es `Tecnico`, coherente con Keycloak; «Técnico» se usa en los textos de interfaz. Los usuarios sin `Administrador` ni `Tecnico`, incluido el rol `Consulta` por sí solo, no reciben acceso a estos casos de uso porque los requisitos no les conceden permisos.

La comprobación de pertenencia debe realizarse también al modificar una tarea por su identificador. Un filtro de técnico enviado por el cliente no puede ampliar el acceso de un usuario técnico.

### A3. Puertos de acceso a datos e identidad

Definir `ITareaMantenimientoRepository` en Application para agregar, obtener, guardar y consultar tareas con los filtros y el alcance de acceso de A4. El contrato no expone `DbContext`, `DbSet`, SQL ni `IQueryable`. Su implementación concreta reside en Infrastructure. No se refactoriza el acceso existente de parques solares como parte de esta funcionalidad.

Reutilizar el contrato de consulta de paneles que resulte de C1; solo si no existe, definir la abstracción mínima necesaria para consultar y comprobar paneles existentes. Del mismo modo, definir tras C2 el contrato mínimo de consulta y validación de técnicos de la fuente aprobada. La integración externa, si es necesaria, se encapsula en Infrastructure; no se incorpora un directorio de usuarios por suposición.

La identidad de `IUsuarioActual` debe corresponder con el identificador del responsable acordado en C2. No utilizar el nombre visible como identidad estable. La obtención de identidad en Blazor Server debe funcionar durante el circuito interactivo y no depender exclusivamente de un `HttpContext` disponible en una solicitud inicial.

### A4. Consultas

Ofrecer consultas por estado `Pendiente` y `EnCurso`, por condición de vencida, por técnico y por panel específico. Los filtros pueden delimitar la misma consulta y nunca sustituyen el alcance de permisos. El administrador puede consultar las tareas de mantenimiento; para el técnico se agrega siempre la restricción de responsable igual al usuario actual.

Las consultas y la recuperación individual respetan ese alcance antes de devolver datos. La referencia temporal para los filtros se obtiene en Application conforme a C6 y se pasa explícitamente a la persistencia y al cálculo de dominio. El filtro de vencidas y el indicador de los resultados aplican la misma definición de D3.

## 3. Infrastructure

### I1. Persistencia e integraciones necesarias

Implementar `TareaMantenimientoRepository` con EF Core 8 y el `ApplicationDbContext` existente. Incorporar el conjunto y la configuración de `TareaMantenimiento`, con sus campos, estado y relación con el panel confirmado. La clave foránea impide guardar una tarea sin un panel existente, complementando la validación de Application.

La representación del responsable, fecha, tipo y observaciones, incluidas nulabilidad y restricciones, sigue las decisiones C1 a C3 y C6. No se crea una clave foránea hacia usuarios inexistentes en MariaDB ni una tabla de catálogo no aprobada. EF Core se configura mediante la API de Infrastructure, sin contaminar Domain.

Las consultas aplican en almacenamiento el alcance del usuario y los filtros solicitados, incluida la definición de vencimiento; evitan cargar todas las tareas para filtrar permisos en la UI. Los adaptadores de paneles y técnicos se implementan o reutilizan únicamente después de confirmar C1/C2.

Generar una migración incremental y actualizar el snapshot cuando el modelo de Panel y su persistencia estén disponibles. Conservar las migraciones existentes; no redefinir el historial de `req003` ni aplicar migraciones automáticamente al iniciar la aplicación. Mantener las cadenas de conexión y los secretos fuera de archivos versionados.

### I2. Protección de escrituras

Al persistir una modificación, comprobar de forma atómica que la tarea siga en el estado que el caso de uso validó y que no esté completada. Para operaciones de un técnico, comprobar también que continúe asignada a ese técnico. Si otra operación cambió esas condiciones, rechazar la escritura y permitir que la interfaz recargue los datos.

Esta protección complementa D2 y A2 para las dos o más instancias previstas por el proyecto: una entidad cargada antes de que otro usuario complete o reasigne la tarea no puede sobrescribirla después. Implementar las condiciones en Infrastructure sin añadir tecnologías, columnas de auditoría o dependencias al dominio.

## 4. Presentation

### P1. Creación, asignación y cambios de estado

Crear una página `MantenimientoPaneles` y un formulario `CrearTareaMantenimiento` con Blazor Server y Radzen. El administrador selecciona un panel existente, informa los datos definidos en D1 y asigna un técnico según C2/C3. La consulta de paneles y técnicos se realiza a través de Application.

Las acciones de iniciar, completar, cancelar y asignar invocan los métodos de `ITareaMantenimientoService`. Las acciones y campos adicionales se incorporan únicamente cuando C4/C5 estén resueltas. Las tareas completadas se muestran sin acciones de modificación; una denegación de Application se presenta como error controlado aunque la vista tuviera datos desactualizados.

Los componentes capturan datos, invocan casos de uso y muestran resultados. No calculan vencimientos, no deciden transiciones de negocio y no acceden a repositorios concretos ni a `ApplicationDbContext`. Tras una operación exitosa recargan los datos; ante un conflicto de I2 informan el problema y permiten actualizarlos.

### P2. Listado y filtros

La página muestra los datos funcionales de las tareas y permite consultar pendientes, en curso, vencidas y tareas de un panel. El administrador puede filtrar por técnico; el técnico visualiza únicamente sus tareas. La representación de fechas sigue C6 y el indicador de vencimiento llega calculado en el DTO.

Proteger rutas y acciones con los roles `Administrador` y `Tecnico` según corresponda, reutilizando el inicio de sesión de Keycloak. Agregar el acceso al menú existente para esos roles. El ocultamiento de acciones complementa la autorización efectiva de A2.

### P3. Composición y contexto del usuario

Registrar el servicio de mantenimiento, sus puertos y adaptadores mediante inyección de dependencias. La implementación de `IUsuarioActual` en Presentation usa el contexto autenticado del circuito de Blazor y expone a Application únicamente identidad y roles. Las implementaciones de persistencia se registran desde Infrastructure y el host las incorpora en su punto de composición, como hace actualmente.

No se modifican el flujo OIDC, los roles existentes, el despliegue, la configuración de Nginx ni el simulador para completar esta funcionalidad.

## 5. Verificación y cierre

### V1. Pruebas de dominio

Crear las pruebas xUnit en `SolarUY.Domain.Tests` junto con cada comportamiento nuevo. Cubrir como mínimo:

- Creación correcta y rechazo de la ausencia de panel, tipo o fecha requerida, con las representaciones acordadas.
- Transición `Pendiente` a `EnCurso` y transición `EnCurso` a `Completada`.
- Rechazo de modificaciones de una completada por todas las operaciones expuestas, conservando los datos anteriores.
- Vencimiento de pendientes y tareas en curso cuya fecha ya pasó.
- Exclusión de completadas, canceladas y fechas que todavía no pasaron del vencimiento.
- Límites temporales y comportamiento de cancelación y edición una vez resueltas C4, C5 y C6.

No se generan nuevas pruebas unitarias de Application, Infrastructure ni Presentation, conforme a las reglas del proyecto.

### V2. Verificación de entrega

Una vez implementadas las decisiones pendientes:

1. Ejecutar `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`.
2. Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` sin modificar sus guardarraíles para obtener un resultado favorable.
3. Aplicar la migración con `dotnet ef database update --project SolarUY.Infrastructure --startup-project SolarUY.Presentation` contra MariaDB con configuración externa y comprobar persistencia y relación con panel existente.
4. Verificar manualmente con usuarios de prueba los flujos de administrador y técnico: creación, asignación, inicio, finalización, cancelación aprobada, filtros y denegación de acceso a tareas ajenas o a usuarios sin permiso.
5. Comprobar que una completada no puede modificarse, que los vencimientos coinciden con la regla aprobada y que una escritura desactualizada tras completar o reasignar no altera la tarea, conforme a I2.

### V3. Documentación final

Al finalizar la implementación, crear `implementations.md` en esta carpeta con los cambios realizados, archivos tocados, decisiones aclaradas y resultados de las verificaciones, indicando expresamente cualquier comprobación no ejecutada y su motivo.
