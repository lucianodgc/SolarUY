# Tareas de implementación: Mantenimiento de paneles

Fuente técnica: specs.md. Las casillas reflejan implementación y verificaciones efectivamente realizadas. Las decisiones C1–C6 se resolvieron mediante el MER y las respuestas del usuario.

## 1. Decisiones y trazabilidad

- [x] 1.1 Adoptar Nodo y Panel del MER como dependencias mínimas de mantenimiento y registrar su implementación y persistencia sin añadir sus altas ni implementar Inversor/LecturaPanel. (C1)
- [x] 1.2 Confirmar Keycloak como fuente de técnicos, sub como ResponsableId y Admin REST API con client_credentials/service account de solo lectura. (C2)
- [x] 1.3 Registrar creación Pendiente, responsable obligatorio, Tipo texto y Descripcion obligatoria para observaciones. (C3)
- [x] 1.4 Registrar cancelación por administrador o técnico asignado desde Pendiente/EnCurso y ausencia de reactivación. (C4)
- [x] 1.5 Registrar campos editables y reasignación exclusivos de administrador en Pendiente/EnCurso; inmutabilidad de terminales. (C5)
- [x] 1.6 Registrar instantes DateTime UTC y vencimiento estrictamente anterior a la referencia. (C6)
- [x] 1.7 Actualizar requisitos, especificación y tareas con el alcance y las decisiones confirmadas. (C1–C6)

## 2. Domain y pruebas

- [x] 2.0 Implementar Nodo, Panel y EstadoPanel del MER como dependencias mínimas, junto con sus pruebas de constructores, relaciones, datos obligatorios, UTC y estado válido. (D1, V1)
- [x] 2.1 Crear EstadoTareaMantenimiento con Pendiente, EnCurso, Completada y Cancelada. (D1)
- [x] 2.2 Crear TareaMantenimiento con identidad, asociación al panel, datos obligatorios y creación Pendiente, junto con pruebas de construcción y rechazo de entradas inválidas. (D1, V1)
- [x] 2.3 Implementar asignación, inicio y finalización con FechaRealizacion UTC, junto con pruebas de transiciones e inmutabilidad de completadas. (D2, V1)
- [x] 2.4 Implementar cancelación, edición y reasignación aprobadas, junto con pruebas de inmutabilidad de canceladas y rechazo sin cambios parciales. (D2, V1)
- [x] 2.5 Implementar EstaVencida con referencia explícita y pruebas de límites, igualdad y exclusión de terminales. (D3, V1)

## 3. Application

- [x] 3.1 Crear DTO de creación, edición, filtros, consulta, paneles y técnicos, con acciones calculadas e instantánea de edición; usar identificador y parámetros para asignación/transiciones sin introducir DTO innecesarios. (A1)
- [x] 3.2 Definir IUsuarioActual y UsuarioActualDto sin dependencias de ASP.NET o Blazor. (A2, A3)
- [x] 3.3 Definir ITareaMantenimientoRepository con consultas restringidas y guardado condicional, sin tipos de EF, SQL o IQueryable. (A3)
- [x] 3.4 Definir IPanelRepository e ITecnicoDirectory con consulta de paneles y listado/validación de técnicos habilitados. (A3)
- [x] 3.5 Implementar ITareaMantenimientoService/TareaMantenimientoService con inyección por constructor y CancellationToken. (A1)
- [x] 3.6 Implementar creación exclusiva de administrador, panel existente y validación de técnico en Keycloak. (A1, A2)
- [x] 3.7 Implementar asignación exclusiva de administrador, validando técnico y estado editable. (A1, A2)
- [x] 3.8 Implementar transiciones, cancelación y edición aprobadas con autorización, pertenencia y comportamiento de dominio. (A1, A2)
- [x] 3.9 Implementar consultas y recuperación individual con filtros, alcance por usuario, referencia UTC única y DTO listos para mostrar. (A2, A4)

## 4. Infrastructure

- [x] 4.1 Agregar conjuntos y configuraciones EF de Nodo, Panel y TareaMantenimiento, relaciones, unicidad y rehidratación UTC. (I1)
- [x] 4.2 Implementar PanelRepository y TareaMantenimientoRepository con contextos por operación, filtros y alcance en almacenamiento. (I1)
- [x] 4.3 Implementar escrituras atómicas condicionadas a estado, responsable y datos originales, con comparación binaria y conflicto controlado. (I2)
- [x] 4.4 Implementar KeycloakTecnicoDirectory: token client_credentials, paginación completa, usuarios habilitados, roles efectivos y errores controlados sin secretos. (I3)
- [x] 4.5 Generar AddMantenimientoPaneles y snapshot incremental, conservando migraciones existentes y configuración externa. (I1)
- [x] 4.6 Incorporar cliente confidencial y service account con view-users/query-users al import, y documentar aprovisionamiento de realms existentes, secreto externo y migraciones. (I3)
- [x] 4.7 Compartir UserSecretsId de Presentation con Infrastructure desde Directory.Build.props, incorporar el proveedor UserSecrets 8.0.1 y hacer que ApplicationDbContextFactory lea los secretos antes de las variables de entorno y rechace una conexión ausente con un error claro. (I1)

## 5. Presentation y composición

- [x] 5.0 Restaurar App.razor del historial y retirar su exclusión concreta de .gitignore para recuperar la compilación del host. (P3)
- [x] 5.1 Implementar UsuarioActualService con AuthenticationStateProvider del circuito y sub/NameIdentifier. (P3)
- [x] 5.2 Registrar servicios, repositorios, directorio, HttpClient y fábrica de contextos conservando las fronteras arquitectónicas. (P3, I1, I3)
- [x] 5.3 Crear MantenimientoPaneles con listado, datos, fechas UTC, filtros y acciones recibidas de Application. (P2)
- [x] 5.4 Crear CrearTareaMantenimiento para administrador con paneles y técnicos obtenidos por Application. (P1)
- [x] 5.5 Conectar creación, edición/reasignación, inicio, finalización y cancelación, con errores, actualización y control de envíos repetidos. (P1)
- [x] 5.6 Proteger páginas, acciones y menú, y agregar AccesoDenegado para la ruta de cookies sin lógica de negocio en vistas. (P1, P2)

## 6. Verificación

- [x] 6.1 Ejecutar SolarUY.Domain.Tests: 36 pruebas aprobadas, incluidos los comportamientos nuevos de mantenimiento y dependencias. (V1, V2)
- [x] 6.2 Ejecutar CodeQuality.Tests: 9 pruebas aprobadas, sin alterar guardarraíles. (V2)
- [x] 6.3 Aplicar migraciones en MariaDB 11.8 temporal con la fábrica de Infrastructure y comprobar persistencia, relaciones y fechas UTC. (V2)
- [x] 6.4 Verificar en navegador creación, edición, inicio, finalización, cancelación, filtros y denegación por rol con usuarios OIDC de prueba; confirmar la pantalla AccesoDenegado sin 404. (V2)
- [x] 6.5 Verificar mediante integración real el alcance de técnico, filtros manipulados, acceso por identificador y denegación a Consulta/anónimos. (V2)
- [x] 6.6 Verificar inmutabilidad, vencimiento y rechazo de escrituras antiguas tras completar o reasignar. (V2, I2)
- [x] 6.7 Verificar directorio contra Keycloak 26.8.1 temporal: paginación, roles directos/compuestos/grupos, usuarios inválidos/deshabilitados y ausencia de permisos de escritura. (V2, I3)
- [x] 6.8 Ejecutar dotnet build y dotnet ef database update con proyectos Infrastructure/Presentation sin --connection, confirmar los identificadores compartidos y el error de conexión vacía de la fábrica; ejecutar los guardarraíles arquitectónicos. (I1, V2)

## 7. Cierre y documentación

- [x] 7. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas. Incluir configuración operativa externa y resultado de verificaciones visuales, y retirar los recursos temporales. (V3)
