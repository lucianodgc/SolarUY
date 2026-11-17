# Especificaciones técnicas: Órdenes al inversor

## 1. Alcance y contratos comprobados

Fuente funcional: `requirements.md` de esta carpeta. Entrega 3. Centralizar en Simulador de inversor las órdenes de activar/desactivar panel y activar/desactivar volcado, con confirmación visual, autorización efectiva de Administrador y auditoría persistente. Conservar .NET 8, EF Core 8, Pomelo 8.0.3, MariaDB 11.8, Blazor Server/Radzen y Keycloak. No agregar un proveedor, bus, ORM ni mecanismo de comunicación distinto del vigente.

Referencias de la base revisada antes de implementar req21 (resultado final en `implementations.md`):

- `IInversorClient` ofrece `ActivarPanelAsync(string nodoId, string panelId, CancellationToken)` y `DesactivarPanelAsync(...)`. Volcado usa `ActivarVolcadoRedAsync(CancellationToken)` y `DesactivarVolcadoRedAsync(CancellationToken)`, sin identificador de nodo/parque. La lectura correspondiente es `ObtenerEstadoVolcadoRedAsync`.
- `InversorTcpClient` implementa ese puerto y utiliza `InversorTcpProtocol`/`InversorTcpServer`. Las órdenes devuelven `Task`, aunque el protocolo devuelve un resultado booleano. Actualmente `EjecutarOrdenAsync` descarta ese booleano; un resultado falso no debe transformarse en éxito de req21.
- `InversorService` ya expone `EstablecerPanelActivoAsync` y `EstablecerVolcadoRedAsync`, y comprueba el rol Administrador. `SimuladorInversor.razor` permite esas operaciones. No existe auditoría específica de órdenes en el código revisado. Las cuatro operaciones existentes deben integrarse con los mismos casos de uso auditados para evitar rutas alternativas sin auditoría.
- El simulador mantiene `_panelesActivados`, `_panelesDesactivados` y un único `_volcadoRedActivo`. La telemetría consulta esas banderas; `MedirPanelRegistrado`, usado por la adquisición histórica, todavía no aplica las banderas de activación/desactivación. Corregir esa divergencia es necesario para verificar la siguiente lectura de 0 W.
- Existen `ParqueSolar`, `Nodo`, `Panel` e `Inversor`. `Inversor.VolcadoRedActivo` pertenece al inventario por nodo; no demuestra que la orden TCP global pueda dirigirse a ese nodo. No actualizar indiscriminadamente esos registros con una confirmación global.
- `UsuarioActualService` obtiene claims autenticados mediante `AuthenticationStateProvider`; `UsuarioActualDto.Id` actualmente permite un fallback a NameIdentifier. Req21 necesita acreditar el `sub` de Keycloak, no atribuir automáticamente ese fallback como si fuera `sub` ni recibir el actor del navegador.
- `MonitoreoHub` y los eventos de lecturas/telemetría existentes actualizan las pantallas; req016 agrega `ObservacionSaludWorker` sobre MariaDB. Reutilizar estos mecanismos y sus grupos autorizados. No convertir el hub en una nueva API de escritura.

Quedan fuera de alcance nuevas órdenes de encendido del inversor, gestión de inventario, usuarios, mantenimientos, exportaciones, pantalla de búsqueda de auditoría y notificaciones externas. La operación de encendido ya existente no se amplía por req21.

## 2. Decisiones acordadas D1–D4

| Id | Decisión confirmada | Regla final |
|---|---|---|
| D1 | Destino global | Volcado al único endpoint configurado; identificar «Inversor configurado — alcance global». No atribuir el flag global a cada inversor del inventario. |
| D2 | Evidencia y coordinación | Solo Exito=true/resultado=true confirma. Resultado falso o rechazo explícito: Rechazada; comunicación incierta después de posible envío: Indeterminada. Sin reenvío automático; correlación por sub y bloqueo de sesión MariaDB por endpoint, sin transacción durante TCP. |
| D3 | Auditoría y recuperación | Actor sub real o nulo, solicitud/destino/UTC/resultado/código acotado. Guardar antes del envío; fallar ese guardado impide enviar. Cierre independiente de 5 segundos y hasta dos guardados; pendientes abandonados se recuperan bajo bloqueo sin reenviar. |
| D4 | Operación y salud | Evidencia operacional consultada o telemetría fechada; desactivar no cambia Panel.Estado, genera 0 W/Sano salvo falla explícita. Conservar precisión histórica y reglas de salud aprobadas. |

D1–D4 están incorporadas como decisiones vigentes en esta especificación: volcado global; ACK válido para confirmar; estados Pendiente/Confirmada/Rechazada/Fallida/Indeterminada; sin reenvío automático; correlación por usuario; bloqueo de sesión MariaDB por endpoint; auditoría anterior al envío y cierre independiente de cinco segundos y hasta dos guardados; recuperación de pendientes bajo bloqueo sin reenviar. La evidencia operacional es la consulta/telemetría fechada, vigente durante tres intervalos; desactivar implica 0 W/Sano salvo falla explícita. Se conserva el estado administrativo y la clasificación por potencia baja continúa deshabilitada.

## 3. Domain

### DOM1. Intento de orden y resultado

Modelar `IntentoOrdenInversor` como registro con comportamiento, identidad Guid generada por Domain y operaciones `ActivarPanel`, `DesactivarPanel`, `ActivarVolcadoRed` y `DesactivarVolcadoRed`. La ampliación responde al registro de auditoría solicitado; no requiere editar el catálogo protegido de `.agents/`.

El intento conserva identidad del actor según D3, operación, destino solicitado/validado, instante de solicitud UTC y resultado con su instante UTC. Para destinos de panel, representar la relación con el inventario cuando existe y conservar la identidad/nombre de destino del momento del intento; una consulta posterior o un traslado no debe reescribir la auditoría. D3 define cómo preservar intentos de identificadores inexistentes sin inventar relaciones a entidades que no existen.

Encapsular los cambios de estado para impedir confirmar un intento sin evidencia de aceptación. El modelo debe distinguir preparación/en curso de resultado terminal y permitir representar un resultado indeterminado. Los nombres finales y transiciones quedan sujetos a D2/D3; no reducir toda excepción a «no ejecutada». Una respuesta confirmada, un rechazo explícito, un fallo que pruebe que no se ejecutó y la pérdida de confirmación son resultados distintos.

La auditoría es inmutable para el usuario: solo los métodos de evolución del propio intento pueden completar su resultado. Conservar solicitud y destino originales; no sobrescribir el actor ni borrar intentos fallidos. Guardar mensajes/códigos seguros y acotados, no objetos de excepción completos ni contenido del transporte sin filtrar.

### DOM2. Semántica operacional y salud

Activar/desactivar expresa un estado deseado, no alternar un flag según potencia. Desactivar correctamente implica que una medición posterior refleje 0 W; activar no garantiza generación positiva ni elimina una falla técnica. El volcado modifica la habilitación global demostrada por el contrato, sin atribuir una redistribución energética o cambios de potencia que el simulador no modele.

No cambiar el estado administrativo de `Panel`, ni marcarlo automáticamente `Falla`, para representar una orden remota. La señal de salud y la habilitación operacional son conceptos distintos. Si D4 exige ampliar evidencia de salud, centralizar su interpretación en `EvaluacionSaludService`, manteniendo los estados y reglas ya aprobados de req016 y la procedencia histórica de lecturas. No reconstruir retroactivamente banderas de operación que no fueron medidas.

### DOM3. Pruebas de dominio

Crear pruebas xUnit simultáneamente con las reglas nuevas: construcción de un intento válido, destinos/operaciones inválidos según D3, UTC, transiciones permitidas y prohibidas, imposibilidad de confirmar sin evidencia y conservación de solicitud/actor. Cubrir las reglas de salud modificadas por D4: cero por desactivación no se transforma en falla y una falla explícita mantiene su significado.

## 4. Application

### APP1. Casos de uso únicos y autorización

Definir `IOrdenInversorService`/`OrdenInversorService` con cuatro operaciones explícitas y un `ResultadoOrdenInversorDto` que informe identidad del intento, resultado conocido y mensaje seguro. Los métodos reciben operación/destino y la correlación que eventualmente acuerde D2; no reciben usuario responsable, roles ni credenciales del navegador.

En cada llamada, obtener la identidad del servidor mediante `IUsuarioActual`, exigir autenticación, rol efectivo `Administrador` y sub válido según D3. Los roles Tecnico/Consulta por sí solos se rechazan antes de enviar al inversor. Si el usuario tiene varios roles, Administrador efectivo habilita la operación. Mantener el mapeo de roles autenticados vigente; no confiar en claims aportados como parámetros.

Exponer el sub real mediante una ampliación compatible del contexto de usuario o un contrato específico de actor de órdenes, según D3, conservando el comportamiento de las consultas que hoy usan Id. La ausencia de sub no se suple con un nombre visible ni un valor inventado; su registro seguro se acuerda en D3.

Validar panel existente y destino vigente a partir del inventario real. Resolver identificadores de nodo/panel requeridos por el puerto desde el servidor y comprobar cualquier pertenencia seleccionada en la vista. No crear inventario ni usuarios. Si el panel cambió de nodo durante la confirmación visual, rechazar/revalidar e informar el nuevo destino antes de enviar, en vez de operar sobre el destino anterior silenciosamente.

### APP2. Ejecución y auditoría

Definir `IIntentoOrdenInversorRepository` para registrar el intento y completar/consultar su resultado. Usar `TimeProvider` para proporcionar UTC al dominio. La orquestación delega reglas al dominio, persistencia al repositorio y comunicación exclusivamente a `IInversorClient`.

Secuencia de ejecución, concretada con D2/D3:

1. Autorizar y validar el destino; registrar los rechazos que D3 incluya, con la identidad realmente disponible y sin enviar al inversor.
2. Registrar de forma durable el intento autorizado antes de emitir la orden. Si falla ese guardado, no enviar: evita un efecto remoto sin registro previo.
3. Aplicar el control compartido de solicitudes/ordenación acordado en D2. El bloqueo visual o un semáforo local no garantizan coordinación entre instancias. No agregar una cola o servicio externo por inferencia ni mantener una transacción SQL abierta durante la espera TCP.
4. Invocar exactamente el método existente correspondiente a la acción solicitada; no duplicar el protocolo ni reemplazarlo con modificaciones a entidades persistidas.
5. Interpretar la evidencia recibida, completar el resultado y persistirlo. Si el envío o el guardado final se interrumpe, mantener visible el resultado no confirmado y aplicar la recuperación acordada, sin reenviar la orden automáticamente por un error de auditoría.

MariaDB y el inversor no comparten una transacción distribuida. La escritura inicial durable permite identificar un intento incompleto tras un reinicio, pero no demuestra qué pasó remotamente. Consultar el estado actual puede ayudar a informar al usuario; no prueba por sí sola que ese intento produjo el cambio, especialmente ante órdenes concurrentes. No presentar una reconciliación de estado como prueba histórica de ejecución.

Separar cancelación antes de enviar, cancelación/timeout después de un posible envío y fallo de persistencia. El registro final no debe depender exclusivamente del token de una página que ya se cerró; aplicar el plazo y política de persistencia acordados en D3. No usar reintentos ilimitados ni dejar tareas sin observar.

### APP3. Compatibilidad e integración de consultas

Hacer que las rutas existentes de `InversorService.EstablecerPanelActivoAsync` y `EstablecerVolcadoRedAsync` deleguen en los casos auditados o sean adaptadas a ellos sin ciclos de dependencias. `OrdenInversorService` invoca el puerto `IInversorClient` directamente a través de inyección; no delega de vuelta al servicio legado. Mantener contratos de lectura y no ampliar la orden de encendido.

Tras un resultado, permitir consultar el estado operacional confirmado con los servicios existentes y actualizar el resultado de la orden. Las lecturas de estado son de solo lectura; no generan otra orden ni crean auditorías de ejecución ficticias. El registro de auditoría no sustituye la lectura del inversor.

La adquisición sigue siendo la fuente de lecturas históricas; no generar una fila artificial de 0 W ni lanzar otra adquisición desde el botón para adelantar el resultado. Diferenciar aceptación de la orden de la fecha de la siguiente muestra que demuestra su efecto. Mantener los eventos de lecturas/telemetría y la observación de salud de req016.

## 5. Infrastructure

### INF1. Persistencia incremental de auditoría

Implementar `IntentoOrdenInversorRepository` y su configuración EF Core. Crear una migración incremental `AddAuditoriaOrdenesInversor` para el almacenamiento del intento y del estado de ejecución definido en D3. No borrar/recrear tablas de inventario, lecturas o salud ni inventar auditoría de operaciones anteriores.

Mapear UTC y enums explícitamente; definir longitudes acotadas para actor, destino, código y mensaje seguro. Las relaciones y datos de destino deben conservar trazabilidad aunque cambie el inventario; no usar cascadas que eliminen auditoría. No crear FK a una tabla Usuario inexistente: Keycloak administra las identidades. Incorporar las restricciones de correlación/concurrencia D2 y los índices mínimos para localizar un intento y recuperar ejecuciones incompletas según D3.

Guardar el resultado con control de versión/estado para que una finalización repetida no reescriba un resultado terminal incompatible. Los reintentos de persistencia no vuelven a invocar el inversor. No agregar retención destructiva, borrado de auditoría o una UI de administración de registros.

### INF2. Puerto y protocolo TCP existentes

Reutilizar `InversorTcpClient`, `InversorTcpProtocol` e `InversorTcpServer`. El alcance global del volcado es el endpoint configurado compartido; las aplicaciones no deben arrancar un simulador privado por instancia para ejecutar sus órdenes. Verificar la misma configuración de destino en A/B sin versionar secretos.

Corregir el descarte del resultado booleano de `EjecutarOrdenAsync`: una respuesta con resultado falso no debe terminar como éxito. Mantener la validación de `Exito`, presencia y tipo del resultado. Identificar rechazos remotos explícitos mediante evidencia estructurada; no deducir que no se ejecutó por el texto de una excepción de comunicación. Si D2 necesita distinguir errores hoy agrupados en `ErrorComunicacionInversorException`, acordar la ampliación mínima de tipos/códigos manteniendo compatibilidad, sin introducir otro transporte.

Una conexión fallida, un timeout después de escribir o una respuesta inválida pueden aportar evidencias diferentes sobre si se emitió la orden. No prometer que el adaptador actual siempre conoce esa diferencia. Documentar las categorías realmente demostrables y preservar la incertidumbre cuando no se pueda probar el resultado.

### INF3. Coherencia del simulador y de las lecturas

Unificar la interpretación de las banderas de operación del simulador para `ObtenerEstadoPanelesAsync` y `ObtenerMedicionesRegistradasAsync`. La desactivación de un panel registrado debe conservarse tras `SincronizarInventarioAsync` si el panel sigue perteneciendo al mismo nodo y producir 0 W en la siguiente adquisición; volver a activar respeta las condiciones reales de encendido, estado administrativo y falla técnica. Mantener la precisión vigente de las mediciones históricas.

No resolver esta divergencia cambiando `Panel.Estado` desde un comando ni forzando `EstadoSaludLectura.Fallado`. Conservar la señal de falla explícita existente y concretar D4 para el resto de la evidencia operacional. La lectura histórica no debe atribuir a muestras antiguas el estado de habilitación observado ahora. No alterar fórmulas de generación o volcado más allá de lo que requieren los contratos reales.

## 6. Presentation

### PRE1. Controles y confirmación

Usar exclusivamente `SimuladorInversor.razor` para los controles Radzen de las cuatro operaciones. `Monitoreo.razor` no contiene órdenes ni el componente retirado de operación; ofrece un enlace al simulador. Mostrar controles de emisión solo al Administrador efectivo; Tecnico y Consulta conservan la consulta de monitoreo y salud. La autorización en Application sigue siendo obligatoria aunque la vista oculte botones.

Usar confirmación explícita mediante el mecanismo Radzen disponible antes de invocar cada caso de uso. Identificar acción, panel/serie y nodo/parque cuando corresponda. En volcado, indicar el ámbito global del inversor configurado aprobado en D1 y advertir de forma concreta que el parque seleccionado no delimita esa orden. Cancelar el diálogo no envía la orden.

Los botones no deben inferir habilitación del panel a partir de 0 W ni de `Panel.Estado`. Obtener el estado operacional mediante contratos de consulta/telemetría, preservando su fecha y disponibilidad; adaptar el DTO de presentación si el mapper actual descarta `EstaActivo`. Cuando no pueda conocerse el estado, informarlo sin simular un cambio.

Deshabilitar envíos repetidos mientras la solicitud esté en curso, conservar la identidad del destino confirmado y descartar respuestas correspondientes a selecciones anteriores. El formulario presenta el resultado seguro y la identidad del intento cuando exista. El estado optimista de un botón no confirma la ejecución.

### PRE2. Resultados, entradas existentes y tiempo real

Mostrar confirmada/rechazada/fallida/indeterminada según el resultado real de Application y el modelo aprobado. Si hubo confirmación remota pero no se pudo completar auditoría, informar esa diferencia según D3; no invitar a reenviar ciegamente ni afirmar un rollback remoto. Exponer mensajes seguros sin credenciales, tokens, trazas o contenido TCP crudo.

Adaptar los cuatro controles ya existentes de `SimuladorInversor.razor` a los casos auditados y a confirmación visual, evitando un camino de emisión sin auditoría. Mantener su consulta y la función de encendido existente dentro de sus contratos actuales.

La confirmación permite refrescar estado operacional de solo lectura, pero la actualización de potencia proviene de las siguientes mediciones por el mecanismo SignalR actual. No insertar puntos falsos en el gráfico ni clasificar salud en Razor/JavaScript. Reutilizar suscripciones, autorización, reconexión, grupos e infraestructura aprobados para req014/req016, sin otro canal de órdenes en el hub ni un sistema de distribución paralelo.

### PRE3. Composición y configuración

Registrar casos de uso/repositorios en el punto de composición con inyección por constructor y ciclos de vida apropiados. Los componentes solo conocen Application. Mantener el endpoint TCP y las credenciales externos; no agregar paquetes o actualizar versiones. Externalizar y validar solo los parámetros de ejecución/recuperación realmente acordados en D2/D3.

Desplegar la migración mediante un único operador, conservando datos previos. La coordinación de solicitudes entre procesos utiliza el mecanismo acordado en D2 sobre la infraestructura existente; la afinidad Nginx no es autorización ni deduplicación. Documentar compatibilidad durante reemplazo de una instancia y el destino de inversor común.

## 7. Verificación prevista

| Id | Verificación |
|---|---|
| V1 | Pruebas xUnit de Domain DOM3, incorporadas con cada comportamiento; ejecutar también las existentes. |
| V2 | Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` después de cambios de código y corregir la implementación, sin modificar guardarraíles. No crear pruebas unitarias nuevas fuera de Domain. |
| V3 | Integración de los cuatro casos de uso con Application, persistencia real y simulador TCP: método/destino correctos, ACK válido, resultado falso, rechazo, respuesta inválida, pérdida de respuesta y cancelación según D2. Ningún resultado no confirmado se presenta como ejecutado. |
| V4 | Autorización mediante llamadas directas: Administrador, Tecnico, Consulta, anónimo, combinación de roles y ausencia de sub; comprobar que los rechazados no envían órdenes y se auditan conforme a D3. El actor persistido procede del contexto del servidor. |
| V5 | Auditoría real MariaDB: actor, acción, destino, solicitud/resultado UTC y mensajes seguros; éxito y error; fallo del guardado inicial sin envío, fallo final sin reenvío y recuperación de intentos incompletos. Verificar migración sobre datos previos y ausencia de secretos o atribuciones ficticias. |
| V6 | Desactivar mediante el caso de uso, ejecutar el circuito normal de adquisición y comprobar 0 W en la lectura y en monitoreo; volver a activar sin exigir potencia positiva. Verificar salud D4 y que sincronizar inventario no borra la orden vigente del mismo destino. |
| V7 | Probar ambas órdenes de volcado y lectura del flag global, su ámbito real y semántica de estado ya alcanzado; la selección de otro parque no cambia el destino global. No exigir cambios de potencia que el simulador no modela. |
| V8 | Navegador: confirmación/cancelación de las cuatro acciones, identidad/ámbito del destino, bloqueo de doble clic, mensajes de resultado y actualización automática por SignalR; probar también los controles existentes del simulador, cambio de selección y reconexión sin repetir órdenes. |
| V9 | Dos instancias detrás del Nginx existente y un endpoint TCP común: emitir desde cualquiera, observar cambios en la otra, comprobar auditoría/concurrencia/repetición según D2 y recuperación de resultados inciertos al reemplazar una instancia. No dar por suficiente un bloqueo de UI o un semáforo de proceso. |

Usar los proyectos y herramientas de integración/navegador existentes. No convertir las pruebas de casos de uso en nuevas pruebas unitarias de Application. Ejecutar las migraciones de prueba sobre bases aisladas; registrar cualquier comprobación no ejecutada y su causa. Esta generación solo crea documentación, no aplica migraciones ni ejecuta órdenes sobre el inversor.

## 8. Cierre y documentación

Al implementar, crear `implementations.md` en esta carpeta con acuerdos D1–D4, comportamiento de confirmación/reintentos, modelo y límites de auditoría, archivos tocados, migración incremental, configuración externa y pasos reproducibles de las cuatro órdenes. Registrar resultados V1–V9, destino común de dos instancias, compatibilidad de entradas existentes y comprobaciones omitidas. Mantener requisitos, specs y tareas coherentes con el alcance aprobado.
