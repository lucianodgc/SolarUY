# Especificaciones técnicas: gestión de nodos e inversores

## Alcance, dependencias y decisiones confirmadas

Fuente funcional: `requeriments.md`. Esta especificación respeta el MER de `.agents/rules/9_modelo_de_datos.md`, .NET 8, Blazor Server con Radzen, EF Core 8/Pomelo y MariaDB 11.8 LTS. No incorpora MediatR, nuevas bibliotecas, APIs ni proveedores de identidad.

`Nodo` ya existe como dependencia de mantenimiento, pero este requerimiento incorpora sus casos de uso de gestión y el inversor persistido asociado. El simulador definido en `req005` conserva su topología e identificadores propios y no se vincula automáticamente con estos inversores.

Las siguientes decisiones fueron confirmadas por el equipo el 29 de octubre de 2026:

| Id | Duda que debe resolver el equipo | Impacto |
| --- | --- | --- |
| C1 | El nodo usa solamente `Nombre`, `UbicacionEnParque` y `CantidadPanelesPrevista` del MER. | No se agregan parámetros técnicos adicionales. |
| C2 | El inversor se crea obligatoriamente junto con el nodo. | El alta recibe los datos de ambos y no permite seleccionar un inversor existente. |
| C3 | Los estados son `Encendido` y `Apagado`. | Se persisten mediante `EstadoInversor`; este alcance solo registra y muestra el estado. |
| C4 | Al eliminar un nodo se eliminan su inversor, paneles, tareas de mantenimiento y lecturas. | La operación se ejecuta transaccionalmente y conserva la regla de no eliminar el último nodo. |

El simulador definido en `req005` sigue aislado del inventario persistido.

## 1. Domain

### D1. Nodo y relación con el parque

Completar el comportamiento de `Nodo`, manteniendo su identidad `Guid` generada por el dominio, su asociación obligatoria con `ParqueSolar` y los atributos del MER: `Nombre`, `UbicacionEnParque` y `CantidadPanelesPrevista` opcional. El constructor y una operación de actualización validarán asociación y textos obligatorios antes de modificar estado. `CantidadPanelesPrevista` no representa la cantidad real de paneles, que resulta de los paneles asociados.

El parque debe encapsular la regla de que no puede quedar sin nodos: solo permitirá quitar un nodo si conserva al menos otro. La aplicación cargará el agregado con los nodos necesarios y delegará la decisión al dominio. No se establece unicidad de `Nodo.Nombre` dentro de un parque, longitudes máximas ni validaciones adicionales de cantidad prevista porque el MER no las define.

### D2. Inversor

Crear la entidad `Inversor` del MER con identidad `Guid`, asociación obligatoria uno-a-uno con `Nodo`, `Marca`, `Modelo`, `PotenciaNominalKW`, `Estado` y `VolcadoRedActivo`. El dominio valida asociaciones, textos, potencia nominal positiva y valores definidos de `EstadoInversor`. La asociación uno-a-uno impide que un nodo tenga más de un inversor.

El formulario selecciona el estado inicial; no se agregan operaciones de transición porque el requerimiento solo exige informar el estado. El dominio no depende de EF Core, Blazor, Keycloak ni del simulador.

### D3. Pruebas de dominio

Crear o ampliar pruebas xUnit de dominio simultáneamente con el comportamiento nuevo. Deben cubrir creación y edición válidas de nodo, entradas inválidas, asociación parque-nodo, rechazo de la eliminación del último nodo y eliminación permitida cuando queden nodos. Tras C2--C4, incorporar pruebas de asociación nodo-inversor y de las reglas aprobadas para creación, estado y eliminación, comprobando que un rechazo no altera el agregado.

## 2. Application

### A1. Casos de uso y contratos

Definir servicios de Application, DTOs y abstracciones de repositorio necesarios para crear, consultar por parque, editar y eliminar nodos, sin exponer `DbContext`, `IQueryable`, SQL ni tipos de Infrastructure. Los servicios y repositorios usarán inyección por constructor y operaciones asíncronas con `CancellationToken` cuando siga la convención existente.

Los DTOs de consulta devolverán el nodo y los datos del inversor ya disponibles para mostrar: marca, modelo, estado y `VolcadoRedActivo`. No calcularán reglas de negocio en Blazor. El diseño concreto del DTO de creación/edición y del caso de uso de inversor queda condicionado por C1--C3; no se crea un servicio que gestione inventario de inversores separado del nodo si C2 no lo exige.

La eliminación obtiene el parque y sus nodos, aplica la regla D1 y elimina transaccionalmente el nodo junto a su inversor, paneles, tareas y lecturas conforme a C4.

### A2. Autorización

Reutilizar `IUsuarioActual` y la matriz de `req009`: solo `Administrador` puede crear, editar o eliminar nodos; `Administrador`, `Tecnico` y `Consulta` pueden realizar consultas de solo lectura, incluido el inversor asociado. Cada operación comprueba identidad autenticada, identificador válido y rol permitido antes de acceder o persistir; la protección de UI no la sustituye. Las denegaciones no producen efectos de persistencia.

### A3. Verificación de autorización

Ampliar las pruebas de integración existentes adecuadas para comprobar que Administrador puede ejecutar las escrituras, que Tecnico y Consulta solo consultan, y que anónimos o roles sin permiso son rechazados antes de persistir. Tras C2--C4, cubrir los flujos de inversor y de eliminación que se aprueben. No se crearán pruebas unitarias de Application, Infrastructure ni Presentation.

## 3. Infrastructure

Persistir `Nodo` e `Inversor` mediante configuraciones EF separadas y repositorios que implementen los puertos de Application. La configuración conserva `Nodo.ParqueId` como FK obligatoria y configura `Inversor.NodoId` como FK obligatoria y única, materializando la relación uno-a-uno del MER. La eliminación explícita y transaccional borra primero tareas, lecturas, paneles e inversor, y después el nodo.

Extender el contexto y crear la migración EF incremental `AddInversorNodo`. La migración no incluye cadenas de conexión ni secretos. No se modifica ni se integra el `IInversorClient` simulador de `req005`.

## 4. Presentation

Crear páginas y componentes Blazor con Radzen para listar nodos de un parque, alta y edición, consulta del inversor y confirmación de eliminación. Las rutas y componentes de escritura se protegerán con `[Authorize(Roles = "Administrador")]`; la consulta se habilitará a `Administrador`, `Tecnico` y `Consulta` conforme a req009. `AuthorizeView` solo ajusta menú, botones y acciones visibles y debe usar los mismos roles.

Los formularios capturan los campos que finalmente aprueben C1--C3, invocan exclusivamente servicios de Application y presentan validaciones y errores controlados. La vista de consulta muestra los datos recibidos, incluido estado y volcado a red, sin calcular reglas ni acceder a Infrastructure. La confirmación de borrado llama al caso de uso y comunica el rechazo de la regla del último nodo o de C4 sin presentar éxito.

## 5. Verificación y cierre

Ejecutar las pruebas de dominio que se creen o modifiquen, las pruebas de integración de autorización contempladas en A3 y `dotnet test` sobre `CodeQuality.Tests`. Verificar que la migración se aplica en una base MariaDB configurada externamente y que se conservan la relación uno-a-uno, la FK y las restricciones aprobadas. Cuando exista entorno de interfaz, comprobar los flujos administrativos, la consulta de los tres roles y los mensajes de validación/denegación. Informar explícitamente cualquier verificación que no se ejecute.

