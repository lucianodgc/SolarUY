# Especificación técnica: ABM de paneles y asignación a nodos (req18)

## Alcance y restricciones

Fuente funcional: `requirements.md` de esta carpeta. Se implementará la administración de paneles solares: alta, consulta general, consulta por nodo, edición y baja, manteniendo la asociación obligatoria con un nodo existente. La entidad se usará posteriormente por mantenimiento y monitoreo; este requerimiento no implementa esas funcionalidades ni define reglas que corresponden a ellas.

La solución respeta Clean Architecture, .NET 8, Blazor Server con Radzen, EF Core 8 con Pomelo y MariaDB 11.8 LTS, y autenticación Keycloak. Se reutilizan las entidades, contratos y componentes existentes cuando sean compatibles. No se introducen dependencias, patrones ni tecnologías nuevos.

## 1. Domain

### D1. Panel y su asociación

- Implementar o completar la entidad `Panel` definida en el MER, sin crear una entidad o tabla alternativa. Tendrá identidad `Guid` generada por el dominio, `NodoId`, navegación obligatoria a `Nodo`, `NumeroSerie`, `Marca`, `Modelo`, `PotenciaNominalW`, `FechaInstalacion` en UTC y `Estado`.
- El constructor y los métodos de modificación encapsularán la validación de los datos aprobados y preservarán la invariancia de que un panel pertenece a un único nodo existente. La lógica de negocio no se ubicará en Application ni en componentes Blazor.
- Se conservarán los valores de estado ya definidos en el MER (`Activo`, `Inactivo`, `Falla`) solo como catálogo de valores. Todas las transiciones entre esos tres estados están permitidas según C2.
- No se inventarán límites de longitud, precisión decimal, formatos adicionales ni reglas de validación para atributos cuyo detalle no figure en el requerimiento o el MER.

### D2. Cantidad de paneles y baja

- El alta, la reasignación autorizada y la baja deberán validar la cantidad resultante de paneles de cada `Nodo` contra `Nodo.CantidadPanelesPrevista`. La cantidad configurada es máxima; null no impone límite, según C1.
- Una operación rechazada no modificará el panel ni los nodos involucrados. El traslado está permitido y valida origen y destino como una única operación.
- La baja observará la política aprobada en C4 respecto de `TareaMantenimiento` y `LecturaPanel`. Se utiliza baja lógica y se conservan las relaciones históricas.

### D3. Pruebas de dominio

Al implementar comportamiento de dominio, crear pruebas xUnit simultáneas para construcción, datos inválidos, asociación obligatoria, actualización, estados aprobados y regla de cantidades. Incluir traslado y baja lógica según C3 y C4.

## 2. Application

### A1. Casos de uso y contratos

- Definir o completar `IPanelService` y `PanelService` para alta, obtención por identificador, listado general, listado por nodo, edición y baja.
- Definir DTOs de entrada y salida que contengan los datos de `Panel` necesarios para cada operación y la identificación/datos de visualización del nodo asociado. No exponer entidades de Infrastructure, `DbContext`, `IQueryable` o SQL.
- Definir o completar `IPanelRepository` como puerto de acceso a paneles. Reutilizar el contrato de nodos existente para verificar la existencia del nodo y obtener la información requerida, sin crear nodos o parques implícitamente.
- Cada caso de uso validará entrada y existencia de panel/nodo, delegará las reglas de negocio al dominio y devolverá resultados o errores controlados de validación, inexistencia y conflicto. La operación de escritura deberá preservar la atomicidad indicada en D2.

### A2. Autorización

- Las operaciones de alta, edición y baja exigirán usuario autenticado con rol efectivo `Administrador` mediante el contexto de usuario existente. La comprobación se hará en Application antes de efectos de persistencia; ocultar botones no la sustituye.
- Administrador, Tecnico y Consulta pueden consultar; solo Administrador puede escribir.

## 3. Infrastructure

### I1. Persistencia

- Implementar `PanelRepository` en Infrastructure mediante EF Core 8/Pomelo y MariaDB, con inyección por constructor y registro en el punto de composición existente.
- Configurar o reutilizar el mapeo de `Panel`, la FK obligatoria `NodoId`, la relación con `Nodo`, la unicidad de `NumeroSerie` y el almacenamiento UTC de `FechaInstalacion`, según el MER.
- Crear una migración EF Core incremental solo si el esquema existente no contiene lo necesario para el alcance aprobado. La migración no recreará tablas ni eliminará datos, lecturas o tareas de mantenimiento.

### I2. Consistencia de escrituras

Las verificaciones de cantidad y las escrituras correspondientes se realizarán de manera atómica y con coordinación adecuada para que operaciones concurrentes sobre un nodo no incumplan la regla que se apruebe en C1. El mecanismo se definirá usando la infraestructura existente; la validación exclusiva en la interfaz no es suficiente.

## 4. Presentation

### P1. Pantallas y comportamiento

- Crear o completar páginas/componentes Blazor Server con Radzen para el listado general y la consulta por nodo, mostrando claramente el nodo de cada panel.
- Incorporar formularios de alta y edición para los campos aprobados, selección de nodo y mensajes claros de éxito, validación, inexistencia y conflicto. El formulario permite traslado con capacidad disponible según C3.
- Incluir confirmación de baja para Administrador y presentar el resultado real de la operación, sin informar éxito cuando Application la rechace.
- Proteger rutas, acciones y controles según A2. Los componentes consumirán exclusivamente servicios de Application y no contendrán cálculos de cantidades ni reglas de negocio.

## 5. Verificación prevista

- V1. Ejecutar las pruebas xUnit de Domain creadas o afectadas por D3.
- V2. Comprobar los flujos de alta, consulta general, consulta por nodo, edición y baja, incluyendo asociación visible, rechazos y ausencia de cambios parciales.
- V3. Verificar autorización: Administrador para escrituras y los permisos de consulta que se confirmen en C5; las denegaciones no producen persistencia.
- V4. Ejecutar `dotnet test` sobre el proyecto `CodeQuality.Tests` después de cambios de código, corrigiendo el código fuente si incumple guardarraíles y sin modificar dichas pruebas.
- V5. Comprobar en la interfaz los listados, formularios, confirmación y mensajes. Si se crea una migración, verificar su aplicación en una base MariaDB configurada externamente.

## 6. Decisiones vigentes

| ID | Decisión resuelta | Impacto |
| --- | --- | --- |
| C1 | Resuelto: `CantidadPanelesPrevista` representa la capacidad máxima; `null` significa capacidad sin límite. | Alta y traslado validan el máximo dentro de una transacción y con bloqueo de nodo. |
| C2 | Resuelto: los estados son `Activo`, `Inactivo` y `Falla`; todas las transiciones entre ellos están permitidas. La potencia debe ser positiva y la fecha UTC. | Define las invariantes y pruebas de `Panel`. |
| C3 | Resuelto: se permite traslado si el nodo destino existe y tiene capacidad. La asociación se actualiza sin alterar las lecturas ni mantenimientos del panel. | Habilita edición de asociación y conserva el historial por `PanelId`. |
| C4 | Resuelto: la baja es lógica. | Los registros de lecturas y mantenimiento se conservan; el panel dado de baja no aparece en los listados operativos. |
| C5 | Resuelto: se reutiliza la matriz vigente: `Administrador`, `Tecnico` y `Consulta` consultan; solo `Administrador` escribe. | Define la protección de servicios, rutas y controles. |

## 7. Cierre documental

Al finalizar una implementación aprobada, crear o actualizar `implementations.md` con los cambios realizados, archivos tocados y resultados reales de las verificaciones; crear o actualizar `resumen.md` con una síntesis breve y fácil de leer. La generación de esta especificación no constituye implementación funcional.


## Protección del historial al eliminar inventario

La eliminación física de un nodo o parque con estado, transiciones o alertas de salud se rechaza antes de modificar dependencias y conserva todo el historial. Para retirar paneles se utiliza la baja lógica de req18. Si no existe historial de salud, la baja administrativa mantiene el comportamiento transaccional previo y elimina también los inversores asociados.
