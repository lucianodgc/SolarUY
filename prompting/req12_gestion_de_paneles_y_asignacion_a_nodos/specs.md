# Especificación: gestión de paneles y asignación a nodos (req12)

## Alcance y estado

Fuente funcional: `requeriments.md` de esta carpeta. Se especifican alta, consulta general y por nodo, edición y baja de paneles, con formularios Radzen, autorización y consistencia entre paneles registrados y configuración del nodo. Esta especificación no acredita implementación ni resuelve las decisiones pendientes de la sección 7 del requerimiento.

Se reutilizan las entidades `Panel` y `Nodo`, la persistencia de MariaDB y la identidad de Keycloak existentes. El modelo actual de `Panel` ya contiene identificador, nodo, número de serie, marca, modelo, potencia nominal en W, fecha de instalación UTC y estado. `Nodo.CantidadPanelesPrevista` es actualmente opcional. Tener esas entidades no equivale a tener implementado el ABM solicitado.

## 1. Domain

### D1. Datos y relación

- Extender el comportamiento de `Panel` existente, sin crear otra entidad de panel ni duplicar tablas. Conservar el `Guid` generado por el dominio y la relación obligatoria con un único `Nodo` mediante navegación y `NodoId`.
- Mantener modelo, potencia nominal, fecha de instalación y estado; conservar número de serie y marca, ya exigidos por el modelo existente. Reutilizar las validaciones de datos obligatorios, unicidad de serie y fechas UTC vigentes.
- Encapsular las modificaciones y validaciones de negocio en métodos del dominio. Application coordina las operaciones; Blazor no decide si una modificación es válida.
- No establecer nuevas longitudes, límites de potencia, estados ni conversiones de unidad sin resolver las aclaraciones correspondientes.

### D2. Consistencia con el nodo

Cada alta, edición que afecte la asociación y baja debe validar la cantidad resultante de paneles respecto de la configuración del nodo. Una operación rechazada debe conservar ambos sin cambios. El criterio exacto de aceptación depende de C1: no se sustituye por una validación de capacidad máxima mientras el requerimiento también exija coincidencia exacta.

Si se autorizan traslados, validar los dos nodos y aplicar la modificación completa de forma atómica. Mientras C2 esté pendiente no se diseña el traslado como una funcionalidad aprobada. La política de baja y conservación de dependencias queda condicionada a C4.

## 2. Application

### A1. Contratos y casos de uso

- Definir `IPanelService` y `PanelService` para crear, obtener por identificador, listar, listar por nodo, actualizar y dar de baja paneles.
- Definir DTOs de entrada y consulta con los datos de D1 y la identificación del nodo. La consulta entrega el nodo asociado para mostrarlo en pantalla.
- Definir `IPanelRepository` en Application para el acceso a paneles y las operaciones de persistencia necesarias; implementar el acceso concreto en Infrastructure. Reutilizar los contratos existentes para consultar nodos cuando resulten suficientes.
- Verificar existencia del panel y del nodo antes de operar, validar las entradas, invocar el comportamiento del dominio y devolver errores controlados de validación, inexistencia o conflicto. No crear nodos ni parques automáticamente al crear un panel.
- La baja y cualquier ajuste autorizado de cantidades se coordinan según C1 y C4; la especificación no autoriza actualizar automáticamente `CantidadPanelesPrevista`.

### A2. Autorización

Comprobar `IUsuarioActual` dentro de cada caso de uso antes de acceder a datos o persistir: usuario autenticado, identidad válida y rol permitido. Crear, editar y dar de baja requieren `Administrador`. Para consultar se reutiliza la matriz ya definida en req9: `Administrador`, `Tecnico` y `Consulta`. Ocultar botones no sustituye esta comprobación.

## 3. Infrastructure

### I1. Persistencia y compatibilidad

- Implementar `PanelRepository` mediante EF Core 8 y Pomelo sobre MariaDB 11.8, con inyección por constructor y registro en el arranque existente.
- Reutilizar `Paneles`, su índice único de número de serie, su relación con `Nodo` y la configuración UTC de la fecha de instalación. No generar migraciones que vuelvan a crear estructuras existentes.
- Si las decisiones aprobadas requieren cambios de esquema, generar una migración incremental y conservar datos, lecturas y tareas de mantenimiento existentes. La baja no debe introducir borrados en cascada de esas dependencias por suposición.

### I2. Escrituras consistentes

Comprobar y persistir la operación sobre paneles y, si se autoriza, la configuración del nodo como una unidad atómica. Coordinar escrituras sobre el mismo nodo para que dos operaciones concurrentes no superen juntas la regla aprobada. El mecanismo concreto debe elegirse al resolver C1, sobre las tecnologías existentes, sin agregar servicios externos. Una validación realizada solo en la interfaz o antes de la escritura sin coordinación no satisface este requisito.

## 4. Presentation

### P1. Pantallas Blazor Server con Radzen

- Listado general de paneles y consulta filtrada o agrupada por nodo, mostrando su asociación.
- Formularios de alta y edición para los datos de D1 y la selección de nodo; la edición de asociación se habilita únicamente si C2 la autoriza.
- Confirmación de baja para Administrador y mensajes de validación o conflicto provenientes de Application.
- Proteger rutas y acciones con la matriz de A2. Los roles de consulta no ven controles de escritura.
- Informar carga, resultado y errores sin anunciar éxito ante una operación rechazada. Las pantallas consumen únicamente Application, sin acceder a DbContext ni calcular reglas de cantidades.

## 5. Verificación prevista

Estas comprobaciones se ejecutarán al implementar, después de resolver las aclaraciones; no se declaran ejecutadas al redactar la especificación.

- V1. Pruebas unitarias de Domain: creación válida, datos inválidos, relación con nodo, actualización, estados y consistencia de cantidades según las decisiones aprobadas; incluir traslado si C2 lo permite.
- V2. Pruebas de integración de los casos de uso y persistencia: alta, consulta general y por nodo, edición, baja, conservación de dependencias y rechazo sin cambios parciales. Verificar operaciones concurrentes sobre un mismo nodo y el efecto en ambos nodos si se autorizan traslados.
- V3. Pruebas de autorización de los casos de uso: escritura solo para Administrador; consultas según req9; denegación de identidades inválidas antes de efectos de persistencia. No generar suites unitarias de Application, Infrastructure ni Presentation.
- V4. Comprobación de interfaz: listados, asociación visible, formularios, confirmación de baja, mensajes y accesos por rol.
- V5. Compilación y ejecución de `CodeQuality.Tests`, además de las pruebas de Domain e integración afectadas. Registrar resultados y comprobaciones omitidas, sin modificar guardarraíles para hacerlos pasar.

## 6. Aclaraciones necesarias antes de implementar

| ID | Decisión pendiente | Impacto en el diseño |
|---|---|---|
| C1 | ¿Cantidad exacta o máxima? ¿Cómo se trata un valor previsto nulo y el alta/baja individual si se exige igualdad exacta? ¿Se rechaza la discrepancia o se autoriza ajustar explícitamente la configuración? | Determina D2, la operación atómica y su coordinación concurrente. El requerimiento enumera alternativas y no elige una. |
| C2 | ¿Puede trasladarse un panel? ¿Qué validaciones aplican a origen, destino e historial? | Condiciona la edición de asociación y la validación de ambos nodos. |
| C3 | Confirmar estados y transiciones. El código existente usa `Activo`, `Inactivo` y `Falla`. | Reutilizar esos valores como antecedente no aprueba por sí solo las transiciones del ABM. |
| C4 | ¿Baja física o lógica? ¿Qué sucede con lecturas y mantenimientos asociados? | Las relaciones actuales son restrictivas. No se autoriza borrar historial ni añadir un atributo de baja por suposición. |
| C5 | Confirmar unidad y validaciones de potencia; el código y el MER actuales usan `PotenciaNominalW`. | No asumir Wp ni renombrar o reinterpretar los valores persistidos sin confirmar compatibilidad. |

## 7. Cierre documental

Al completar la implementación, actualizar `implementations.md` con cambios, archivos y verificaciones efectivamente realizados, y `resumen.md` con una síntesis breve. La creación de esta especificación y de sus tareas no cierra el requerimiento funcional.
