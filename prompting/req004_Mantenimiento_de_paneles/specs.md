# Especificaciones técnicas: Mantenimiento de paneles

## Alcance y decisiones confirmadas

Fuente funcional: `requierements.md`, el MER de `.agents/rules/9_modelo_de_datos.md` y las respuestas explícitas del usuario. Se mantienen .NET 8, Blazor Server con Radzen, MariaDB 11.8 LTS, EF Core 8/Pomelo 8.0.3 y Keycloak. Para la corrección de EF CLI solicitada por el usuario se incorpora únicamente Microsoft.Extensions.Configuration.UserSecrets 8.0.1 en Infrastructure. No se agregan tablas de usuarios, credenciales humanas ni MediatR.

| Referencia | Decisión confirmada |
| --- | --- |
| C1 | Usar el MER: identidad Guid, ParqueSolar → Nodo → Panel → TareaMantenimiento. Implementar únicamente Nodo y Panel como dependencias necesarias para mantenimiento, con persistencia y consulta; no añadir altas ni implementar Inversor o LecturaPanel. |
| C2 | Keycloak es la fuente de verdad. ResponsableId guarda sub, de tipo string y sin FK a usuarios. ITecnicoDirectory lista y valida usuarios habilitados con rol efectivo Tecnico mediante Admin REST API, client_credentials y service account confidencial de solo lectura. |
| C3 | Crear siempre Pendiente y con responsable. Tipo es texto; Descripcion obligatoria representa las observaciones. Registrar FechaRealizacion al completar. |
| C4 | Administrador y técnico asignado cancelan desde Pendiente o EnCurso. Una cancelada no se reactiva ni modifica. Las únicas transiciones son Pendiente → EnCurso → Completada y Pendiente/EnCurso → Cancelada. |
| C5 | Solo el administrador edita Tipo, Descripcion, FechaProgramada y ResponsableId en Pendiente o EnCurso. Puede reasignar en esos estados. No cambia el panel de una tarea. Completadas y canceladas son inmutables. |
| C6 | DateTime UTC. Vencida significa FechaProgramada estrictamente anterior a la referencia UTC y estado Pendiente o EnCurso. Igualdad no es vencimiento. Se permite programar una fecha pasada. |

Estas aclaraciones están resueltas. La implementación de las dependencias de mantenimiento no da por implementado el resto de req003 ni los inversores de req005. No se añaden eliminación de tareas, notificaciones, auditoría o trabajos programados.

## 1. Domain

### D1. Entidades y asociaciones

Implementar Nodo y Panel del MER, únicamente con sus atributos definidos, claves Guid generadas en constructor y relaciones de navegación Parque/Nodo. EstadoPanel contiene Activo, Inactivo y Falla. No modificar ParqueSolar ni diseñar los estados o comportamiento de Inversor. La relación de Nodo con Inversor queda para su requerimiento; no se implementa el alta de Nodo en este alcance.

TareaMantenimiento contiene Id, PanelId/Panel, Tipo, Descripcion, FechaProgramada, FechaRealizacion nullable, Estado y ResponsableId. EstadoTareaMantenimiento contiene Pendiente, EnCurso, Completada y Cancelada. Vencida no es un estado almacenado.

Constructores validan asociaciones y textos obligatorios. Fechas obligatorias deben ser instantes UTC válidos. No inventar catálogos, longitudes máximas funcionales, reglas de potencia positiva o restricciones de fecha futura. Entidades independientes de frameworks, con private set.

### D2. Comportamiento

TareaMantenimiento encapsula Iniciar, Completar, Cancelar, AsignarResponsable y Editar. Iniciar exige Pendiente; Completar exige EnCurso y recibe el instante UTC de realización. Cancelar, editar y asignar admiten solo Pendiente o EnCurso. Toda validación ocurre antes de alterar datos para evitar modificaciones parciales.

Completadas y canceladas rechazan toda modificación. Completar conserva la tarea y registra estado y FechaRealizacion. No existen reapertura, cierre directo desde Pendiente ni cambios de panel. Roles y pertenencia se validan en Application.

### D3. Vencimiento

EstaVencida recibe referencia DateTime UTC explícita y aplica C6. No consulta el reloj, persiste un indicador ni cambia estados automáticamente. Infrastructure usa exactamente el mismo predicado para filtrar.

## 2. Application

### A1. Contratos y casos de uso

ITareaMantenimientoService/TareaMantenimientoService exponen creación, asignación, edición, inicio, finalización, cancelación, recuperación individual y listado, además de opciones de paneles y técnicos. Acceso asíncrono, CancellationToken e inyección por constructor.

DTO: CrearTareaMantenimientoDto, EditarTareaMantenimientoDto, FiltroTareasDto, TareaMantenimientoDto, PanelDto y TecnicoDto. La consulta incluye vencimiento y acciones permitidas ya calculadas. La edición incluye una instantánea TareaMantenimientoOriginal para detectar cambios desde la apertura del formulario.

Crear verifica panel existente y técnico válido. Asignar y editar vuelven a validar el responsable en Keycloak. Las modificaciones cargan la tarea con alcance de permisos, invocan su comportamiento y guardan condicionalmente. Errores funcionales y conflictos se exponen mediante MantenimientoException y MantenimientoConflictoException; fallos de directorio mediante TecnicoDirectoryException. No se guardan cambios si falla una validación.

### A2. Autorización efectiva

IUsuarioActual.ObtenerAsync devuelve UsuarioActualDto con autenticación, Id estable y roles sin dependencias de ASP.NET. Los casos de uso rechazan anonimato, identidad ausente y usuarios sin Administrador/Tecnico. Datos de entrada nunca prueban identidad o roles del actor.

Administrador crea, asigna, edita y consulta todas las tareas. Puede iniciar, completar y cancelar según D2. Tecnico consulta y cambia estado exclusivamente de sus tareas; no crea, edita, reasigna ni enumera todo el directorio. Consulta por sí solo no recibe acceso.

Lectura individual y modificaciones por identificador aplican el mismo alcance que los listados. Un filtro enviado por el técnico no amplía ese alcance. Para sus selectores de panel se devuelven solo paneles asociados a sus tareas.

### A3. Abstracciones

ITareaMantenimientoRepository agrega, obtiene, consulta y guarda condicionalmente tareas. IPanelRepository lista y recupera paneles existentes. ITecnicoDirectory lista técnicos habilitados y valida un ResponsableId. No exponen DbContext, DbSet, IQueryable, SQL, HTTP ni credenciales.

La identidad de IUsuarioActual corresponde al sub de Keycloak, admitiendo ClaimTypes.NameIdentifier cuando OIDC lo mapea. Los nombres visibles no se usan como identificadores. No se refactoriza la funcionalidad previa de parques solares.

### A4. Consultas

Filtros combinables por estado, vencimiento, técnico y panel. Application obtiene una única referencia UTC por consulta para la persistencia y los resultados. La pertenencia efectiva se añade siempre para técnicos. DTO de consulta entrega fechas UTC, datos y acciones listos para mostrar.

## 3. Infrastructure

### I1. Persistencia

Extender ApplicationDbContext con Nodos, Paneles y TareasMantenimiento. Configuraciones EF separadas, claves y relaciones del MER y unicidad de NumeroSerie. Relaciones nuevas con borrado Restrict para preservar las asociaciones existentes; no hay casos de uso de eliminación.

Conservar las convenciones de Pomelo para textos y decimal, sin añadir validaciones funcionales de longitud o precisión. El índice único de NumeroSerie obtiene varchar(255) y las potencias decimal(65,30) según esas convenciones. Rehidratar fechas como UTC, incluidas FechaInstalacion, FechaProgramada y FechaRealizacion.

Usar IDbContextFactory por operación para que eventos de un circuito no compartan DbContext. AddDbContextFactory conserva la disponibilidad scoped del contexto para servicios anteriores. PanelRepository y TareaMantenimientoRepository aplican filtros y pertenencia en SQL antes de devolver datos.

Crear migración incremental AddMantenimientoPaneles y actualizar snapshot conservando AddParqueSolar. No aplicar migraciones al arrancar ni versionar cadenas de conexión. EF CLI admite Presentation como startup con su referencia existente a EF Design, y también Infrastructure con su fábrica de diseño.

ApplicationDbContextFactory carga JSON, user-secrets y variables de entorno en ese orden. El identificador público se define una sola vez como SolarUyUserSecretsId en Directory.Build.props; los dos proyectos lo usan en UserSecretsId, conservando el almacén local existente. Infrastructure usa AddUserSecrets<ApplicationDbContextFactory>(optional: true), sin depender del ensamblado de Presentation. Una conexión nula, vacía o de espacios genera un error claro que indica la clave y cómo configurarla, en lugar del fallback string.Empty. Las variables de entorno mantienen prioridad. Verificar dotnet build, el comando solicitado con startup Presentation sin --connection y el caso de configuración vacía usando directamente la fábrica con startup Infrastructure.

### I2. Concurrencia

Guardar con una única actualización SQL condicionada a identidad de tarea, estado editable, estado/responsable/datos originales y, cuando corresponde, técnico autorizado. Comparar textos e identidad con colación binaria para evitar equivalencias por mayúsculas de la colación predeterminada.

Si no hay una fila afectada, comunicar conflicto. Una escritura anterior no sobrescribe una tarea completada, cancelada, reasignada o editada concurrentemente. No agregar columnas de auditoría ni dependencias al dominio. La instantánea de edición nunca reemplaza la autorización confiable del actor.

### I3. Directorio de Keycloak

KeycloakTecnicoDirectory implementa ITecnicoDirectory con HttpClient compartido, autorización por solicitud, sin redirecciones automáticas ni almacenamiento de usuarios/tokens en MariaDB. Configuración Keycloak:Directory:BaseUrl, Realm, ClientId y ClientSecret externo; HTTPS salvo localhost.

Obtener token con client_credentials. Recorrer todas las páginas de usuarios habilitados y comprobar roles efectivos mediante users/{id}/role-mappings/realm/composite, incluyendo grupos y roles compuestos. La validación individual comprueba existencia, enabled e identidad exacta además del rol Tecnico. Fallos de conexión, autenticación, autorización o formato no habilitan asignaciones y generan errores controlados sin cuerpos de respuestas ni secretos.

Cliente solaruy-tecnico-directory: confidencial, service accounts activas, flujos interactivos y de contraseña deshabilitados. Solo view-users y query-users de realm-management; sin permisos de escritura. El import declara cliente y roles de su service account, sin un secreto fijo. Keycloak genera el secreto y se configura fuera del repositorio.

Entornos existentes requieren aprovisionamiento explícito: --import-realm no actualiza un realm ya creado. Documentar preparación, obtención segura del secreto y user-secrets/variables. No usar cuentas administrativas humanas desde la aplicación.

## 4. Presentation

### P1. Formularios y acciones

CrearTareaMantenimiento captura panel existente, tipo, descripción, fecha UTC y técnico habilitado. Solo Administrador accede. MantenimientoPaneles permite editar/reasignar los campos de C5 usando una instantánea del resultado consultado.

Acciones de iniciar, completar y cancelar invocan Application. DTO decide su disponibilidad; las tareas terminales no ofrecen acciones. Recargar tras éxito y permitir actualizar ante errores o conflictos. Mostrar errores controlados sin detalles internos. Deshabilitar envíos repetidos durante operaciones.

### P2. Listado

Página /mantenimiento para Administrador/Tecnico con Radzen y filtros de A4, fechas explícitamente UTC e indicador de vencimiento calculado por Application. Opciones de técnicos solo para administrador. Menú protegido por los mismos roles.

La ruta de denegación de cookies /Account/AccessDenied muestra AccesoDenegado con un mensaje y enlace de regreso, para que el intento directo de un técnico de abrir creación no termine en una página 404.

Vistas capturan eventos, invocan servicios y muestran datos. No deciden transiciones, calculan vencimientos ni acceden a infraestructura. Convertir la fecha escrita como UTC a DateTimeKind.Utc es adaptación de entrada, no cálculo de negocio.

### P3. Composición y contexto

UsuarioActualService implementa IUsuarioActual mediante AuthenticationStateProvider del circuito, sin HttpContext ni copias persistentes de la sesión. Registro scoped y servicios de mantenimiento inyectados directamente desde el circuito, evitando un OwningComponentBase que crearía un AuthenticationStateProvider sin inicializar. La persistencia se aísla mediante la fábrica de contextos de I1.

Registrar adaptadores en Infrastructure y servicios en el host. Restaurar App.razor desde su última versión disponible en el historial y retirar su exclusión de .gitignore: Program.cs todavía lo requiere y su ausencia impide compilar. No rediseñar ese componente ni cambiar OIDC o el despliegue.

## 5. Verificación y documentación

### V1. Pruebas de dominio

Crear pruebas xUnit simultáneamente con constructores y comportamiento nuevos de Nodo, Panel y TareaMantenimiento. Cubrir datos obligatorios, relaciones, UTC, creación Pendiente, transiciones, realización, edición/reasignación válidas, rechazo sin cambios parciales, inmutabilidad de completadas y canceladas y límites de vencimiento.

No agregar pruebas unitarias de Application, Infrastructure o Presentation ni modificar CodeQuality.Tests.

### V2. Verificación funcional y arquitectónica

Ejecutar Domain.Tests y CodeQuality.Tests. Usar salida de compilación separada si la aplicación activa bloquea bin/Debug.

Aplicar dotnet ef database update con proyectos Infrastructure contra MariaDB 11.8 temporal o configurada externamente. Comprobar persistencia, FK, UTC, consultas, permisos por rol/identificador y escrituras antiguas tras completar o reasignar.

Verificar ITecnicoDirectory contra Keycloak 26.8.1 aislado con service account de lectura: paginación, roles directos/compuestos/grupos, usuario inexistente, deshabilitado o sin rol y rechazo de escrituras con esas credenciales. Se permite un ejecutable temporal de comprobación de integración en artifacts ignorados; no forma parte de los proyectos de pruebas unitarias.

Comprobar los flujos visuales de creación, edición, inicio, finalización, cancelación y filtros cuando exista una sesión de prueba y navegador disponible. Informar expresamente cualquier comprobación no ejecutada, sin presentarla como aprobada. No modificar servicios, realm ni datos existentes para probar; retirar los recursos temporales al terminar.

### V3. Cierre

Crear implementations.md con cambios, archivos, decisiones resueltas, comandos y resultados, limitaciones y configuración operativa externa necesaria. Marcar solo tareas realmente completadas y mantener explícitas las verificaciones pendientes.
