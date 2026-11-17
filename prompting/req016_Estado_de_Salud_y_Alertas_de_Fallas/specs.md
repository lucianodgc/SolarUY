# Especificaciones técnicas: Estado de salud y alertas de fallas

## 1. Alcance y situación actual

Fuente funcional: `requirements.md` de esta carpeta. Entrega 3. Incorporar salud calculada de paneles, agregación por nodo, alertas activas e historial de transiciones en el monitoreo existente. Los roles `Administrador`, `Tecnico` y `Consulta` acceden exclusivamente como lectores. No incorporar gestión manual de alertas, mantenimientos, asignaciones ni notificaciones externas.

El diseño conserva .NET 8, EF Core 8, Pomelo 8.0.3, MariaDB 11.8, Blazor Server, Radzen y Keycloak. Application depende únicamente de Domain; Infrastructure implementa contratos de Application; los componentes invocan casos de uso. Usar inyección por constructor y servicios con sufijo `Service`, sin introducir bibliotecas, buses ni servicios externos.

Referencias comprobadas en el repositorio:

- `ParqueSolar`, `Nodo`, `Panel` y `LecturaPanel` son las entidades reales. `Panel.Estado` usa `EstadoPanel` (`Activo`, `Inactivo`, `Falla`); describe un significado distinto de la nueva salud calculada.
- `LecturaPanel.EstadoSalud` usa `EstadoSaludLectura` (`Sano`, `Fallado`, `Desconocido`). `NodoId` histórico es nullable para procedencias antiguas no demostrables. No reescribir estos datos ni reinterpretar ordinales existentes.
- `RecoleccionLecturasService`, `IRecoleccionLecturasRepository` e `IInversorClient.ObtenerMedicionesRegistradasAsync` forman el circuito de adquisición histórica. El lote exige exactamente una medición por panel; no cambiar esta garantía para introducir fallas sintéticas o filas incompletas.
- `InversorSimulador` admite paneles fallados por configuración; reutilizar esa capacidad. No agregar controles de simulación o edición a las consultas de salud.
- La pantalla `Monitoreo.razor`, `MonitoreoHub` en `/hubs/monitoreo` y `NotificadorLecturasConfirmadas` ya existen. También existen `TelemetriaEnVivoWorker` y el evento `TelemetriaActualizada`, cuyo uso requiere distinguir datos en vivo de cambios de salud confirmados.
- La migración `20261105010000_RemoveDistribucionMonitoreo` retiró las tablas del transporte durable anterior. El registro actual contiene notificadores locales; no asumir que siguen existiendo `IDistribucionMonitoreoRepository`, avisos compartidos o cursores de req014. Esta discrepancia debe resolverse mediante D6, sin reinstalarlos unilateralmente.

## 2. Decisiones previas y trazabilidad

No fijar valores arbitrarios ni tratar estas decisiones como aprobadas por la generación del documento. Cerrar cada decisión y actualizar las secciones dependientes antes de implementarlas; el resto del trabajo independiente puede avanzar.

| Id | Decisión que debe acordarse | Resultado necesario |
|---|---|---|
| D1 | Fuente/fórmula de potencia esperada y condiciones de generación normal | Datos realmente disponibles, fórmula y unidades; tratamiento de noche, apagado, desactivación y potencia nominal. No equiparar potencia nominal a potencia esperada sin aprobación. No agregar clima, sensores o servicios externos por suposición. |
| D2 | Clasificación y precedencia | Umbrales configurables, inclusividad de límites, duración/persistencia si corresponde, prioridad de falla explícita frente a ausencia/degradación, plazo sin lecturas, referencia para panel sin primera muestra y manejo de evidencia desconocida/incompleta. |
| D3 | Agregación de salud de nodo | Regla para combinaciones de estados, cobertura insuficiente, paneles excluidos según D1/D2 y nodo vacío. No presumir simplemente el peor estado. |
| D4 | Compatibilidad y arranque | Correspondencia entre `EstadoSaludLectura`, señal del inversor y salud calculada; significado de `EstadoPanel`; primera evaluación, estado anterior inicial, momento de cambio frente a detección, lecturas del nodo anterior y paneles trasladados. No inventar estados históricos. |
| D5 | Ciclo de alertas | Condiciones que abren alerta, relación entre degradación/falla/desconexión, prioridad o coexistencia, cierre/reactivación e intermitencia. Determinar si cada recurrencia abre un episodio nuevo o reabre el anterior y si cambios de política provocan transiciones. |
| D6 | Integración vigente de alta disponibilidad | MariaDB compartida y observadores por instancia; acuerdo cerrado mediante la validación y delegación de Luciano comunicadas por el usuario. A/B candidatas de adquisición con exclusión compartida, evaluación/observación activas y relevo sin promoción manual; B independiente de la salud de A. No agregar transporte ni restaurar tablas retiradas. Ver `coordinacion_despliegue.md`. |

## 3. Domain

Acuerdo de implementación: el usuario confirmó D1–D5 de `propuesta_decisiones.md`. La clasificación por potencia baja queda deshabilitada inicialmente hasta contar con evidencia de operación; se conservan umbrales configurables 50 %/80 %, desconexión tras tres intervalos y evaluación cada cinco segundos. La prioridad del nodo es Falla > Desconectado > Degradado > Normal, con cobertura explícita. La primera transición tiene anterior nulo; existe una alerta activa por panel y cada cambio o recurrencia abre otro episodio después de cerrar el previo. Posteriormente, al solicitar completar los pendientes y responder «hazlo lo mejor posible», el usuario autorizó avanzar con la propuesta D6 mediante MariaDB existente. Posteriormente el usuario informó la validación de Luciano y su delegación de la elección técnica: se cerró la coordinación el 17/11/2026 con ambas instancias candidatas a adquirir, exclusión MariaDB y relevo sin promoción manual, según `coordinacion_despliegue.md`. No se atribuye una fecha original de validación ni un despliegue real ejecutado.

### D6. Diseño autorizado de observación compartida

Incorporar `IObservacionSaludRepository` en Application y `ObservacionSaludRepository` en Infrastructure. Dos consultas en una instantánea RepeatableRead obtienen parques (incluidos vacíos) y panel, nodo/parque actuales, presencia de salud, versión por panel, instante de evaluación, nodo evaluado y suficiencia. No adquieren mediciones, no alteran salud ni instalan tablas de transporte. Cada instancia ejecuta `ObservacionSaludWorker` con seguimiento en memoria del último snapshot entregado a su hub local. Al arrancar o reiniciar, invalidar todos los parques actuales, incluso si quedaron sin paneles durante la caída; un aviso de parque completo tiene PanelId nulo. Esto evita retener una selección antigua por no conocer la topología previa al reinicio.

Comparar firmas por panel; no usar un MAX global ni un cursor temporal que pueda saltar commits. Una modificación invalida parques anterior y actual. Una retirada invalida el parque anterior y libera su entrada. Confirmar el snapshot únicamente después de enviar todas sus invalidaciones: un error deja el anterior para reintentar. Un reinicio vuelve a avisar el inventario completo. Las consultas de carga/reconexión reconstruyen salud, alertas e historial abierto. El historial conserva todas las transiciones, mientras el transporte converge al estado vigente sin prometer reproducción individual de cada aviso ni entrega exactamente una vez.

Externalizar `SaludDistribucion:InstanciaId`, `IntervaloSegundos` (1 por defecto) y `Habilitada`; validar al arranque y registrar el identificador en `X-SolarUY-Instancia` para comprobar afinidad. El identificador debe ser distinto por proceso en despliegue doble. La memoria del observador es proporcional al inventario actual. Agregar `SaludPanel:Habilitada` para permitir instancias que solo observen; la evaluación concurrente sigue siendo idempotente y serializada en MariaDB. No mantener transacciones durante el envío al hub.

Verificar específicamente fallo de envío sin avance, caída/reinicio de observador, nuevas versiones durante una lectura, traslado entre parques y cierres. Ejecutar V8 con evaluador/adquisición solo en una instancia y un observador en cada una, para demostrar que el segundo cliente no depende de reevaluación local. Compartir claves de Data Protection y nombre de aplicación, mantener afinidad y reemplazar una instancia por vez mediante el Nginx existente.

### DOM1. Clasificación y agregación

Definir `EstadoSaludPanel` de Domain con `Normal`, `Degradado`, `Falla` y `Desconectado`. Existe un enum homónimo en `Application.Abstractions` con `Saludable`/`Fallado`: distinguirlos por namespace o adaptar nombres internos sin mezclar ni cambiar sus valores persistidos/contratos de inversor por accidente.

Crear `EvaluacionSaludService` y `PoliticaSalud` en Domain. Recibir evidencia de medición, instante UTC de referencia explícito y parámetros aprobados en D1/D2/D4; devolver clasificación, motivo, fecha de evidencia y suficiencia/vigencia. No consultar reloj, EF, configuración ni inversor desde Domain. Validar unidades W, rangos y coherencia de la política.

La potencia baja solo participa cuando D1 permite esperar generación. Cero, falta de comunicación y estado administrativo son conceptos distintos. No convertir `Desconocido` en `Normal` por defecto ni añadir un quinto estado de negocio: la insuficiencia de evidencia se representa separadamente y su consecuencia se decide en D2. Un estado previo puede conservarse como último conocido, pero no presentarse como una evaluación vigente.

Crear `SaludNodoService` para agregar los resultados de los paneles actuales con D3, entregando estado, cobertura y suficiencia. No almacenar el total de salud del nodo como nueva fuente de verdad ni mezclarlo con los agregados de potencia de req014.

### DOM2. Estado persistido, transiciones y alertas

Modelar `SaludPanel`, `CambioEstadoSaludPanel` y `AlertaPanel` como conceptos de esta funcionalidad, con identidad Guid generada por Domain y relaciones con `Panel`. Estas incorporaciones responden a la persistencia de estados, transiciones y alertas requerida; no implican modificar el catálogo de entidades de `.agents/`.

`SaludPanel` conserva el último estado confirmado, motivo, referencia de evaluación/evidencia y control de versión. Encapsular la aplicación de un resultado: mantener el mismo estado no crea una transición; una evaluación atrasada no sustituye indebidamente una posterior. Puede actualizarse la evidencia sin agregar historial cuando el estado no cambia.

`CambioEstadoSaludPanel` conserva panel, estado anterior/nuevo y fecha UTC del cambio. La representación del estado anterior de la primera evaluación depende de D4; no registrar un `Normal` ficticio. D4 distingue fecha de medición, momento efectivo del cambio y fecha de detección. Mantener orden estable aun si dos cambios comparten precisión temporal, mediante versión por panel.

`AlertaPanel` conserva identidad de condición/episodio, panel, tipo o estado causante, detección UTC y cierre UTC cuando corresponda. Encapsular su ciclo aprobado en D5. Una condición sostenida no crea otra alerta activa; el cierre no elimina ni modifica transiciones anteriores. Resolver una condición y abrir otra se decide con el nuevo resultado de dominio, no desde la vista.

El historial conserva la identidad del panel; las consultas de alertas muestran nodo/parque actuales. La procedencia histórica y los traslados se tratan según D4, sin atribuir silenciosamente una lectura antigua a un nodo nuevo. No ampliar en este requerimiento las operaciones de eliminación de paneles; las relaciones nuevas deben impedir borrados en cascada del historial.

### DOM3. Pruebas de dominio

Crear pruebas xUnit simultáneamente con cada comportamiento: clasificaciones, límites aprobados, baja potencia normal fuera de generación, falla explícita, evidencia incompleta, ausencia de datos y desconexión; agregación D3; estado administrativo separado; primera evaluación; repetición, orden temporal y transición; apertura, cierre y recurrencia D5. Ninguna prueba debe cristalizar una decisión todavía pendiente.

## 4. Application

### APP1. Evaluación automática y recuperación

Definir `ISaludPanelService`/`SaludPanelService` para orquestar evaluaciones y `ISaludPanelRepository` para obtener evidencia y confirmar cambios. La clasificación y el ciclo de alertas residen en Domain. Usar `TimeProvider` para suministrar referencias UTC y propagar cancelación.

Procesar únicamente lecturas históricas confirmadas por el circuito existente; nunca derivar un historial durable directamente de `TelemetriaActualizada` sin el acuerdo D4. Integrar después del commit, sin introducir otra llamada al inversor. Si falla el procesamiento de salud después del guardado, conservar la lectura y permitir reintentar su evaluación sin repetir la adquisición.

Reevaluar también la ausencia de lecturas aunque no haya nuevos eventos. Incorporar una ejecución periódica de evaluación de salud, cuyo intervalo sea configurable y coherente con D2; consume evidencia persistida y tiempo, sin adquirir mediciones. Integrarla con los ciclos y coordinación D6. Evaluar al arrancar y reconciliar trabajo pendiente tras fallos, sin depender exclusivamente de un callback volátil.

Reintentos y evaluaciones concurrentes deben releer evidencia/estado al confirmar. Distinguir orden de evidencia, versión del estado e instante de evaluación: una lectura recibida después de una desconexión puede justificar recuperación; no descartarla mecánicamente por ser anterior al reloj de la evaluación. No permitir que trabajo calculado sobre un snapshot antiguo revierta una decisión más reciente.

### APP2. Consultas autorizadas

Definir `IConsultaSaludService`/`ConsultaSaludService` y DTO `SaludPanelDto`, `SaludNodoDto`, `AlertaActivaDto` y `CambioEstadoSaludDto`. Entregar estado, motivo, vigencia/suficiencia, panel/nodo/parque identificables y fechas UTC. La vista recibe resultados listos; no calcula umbrales, precedencias, agregados o cierres.

Exponer consulta de salud del parque seleccionado, alertas activas del mismo parque e historial paginado por panel, con orden estable por fecha y versión. Validar autenticación, uno de los tres roles, identificadores, existencia y pertenencia a la selección antes del acceso a datos; no asumir que los roles actuales imponen restricciones por propietario no definidas. Aplicar límites de página positivos y acotados, sin cargar todo el historial. No agregar búsquedas o exportaciones fuera del alcance.

Los procesos internos de evaluación no dependen de credenciales de un usuario conectado. Sus operaciones de escritura no se exponen como comandos de interfaz ni métodos de hub.

### APP3. Notificaciones confirmadas

Definir `INotificadorSaludConfirmada` y avisos de invalidación por parque/panel afectados, con versión suficiente para reconciliar cambios. Publicar únicamente después del commit de salud/transición/alertas, a través del mecanismo confirmado en D6 y el hub existente. Un fallo de notificación no revierte el guardado ni dispara otra adquisición.

Repetir u omitir temporalmente un aviso no puede producir estados falsos: la carga inicial, la resuscripción y la reconciliación recuperan el estado persistido. Notificar aperturas, cambios y cierres, incluidas desconexiones detectadas sin nueva lectura. Las consultas usan la topología actual; considerar los parques afectados por traslados conforme a D4. No afirmar entrega exactamente una vez.

## 5. Infrastructure

### INF1. Mapeo y migración incremental

Implementar `SaludPanelRepository` y mapeos EF separados para los tres conceptos. Conservar `LecturaPanel.EstadoSalud`, sus ordinales, `Panel.Estado`, relaciones e índices históricos. La migración `AddEstadoSaludAlertas` agrega el almacenamiento de salud sin borrar filas previas, reconstruir el esquema ni rellenar transiciones retrospectivas sin evidencia.

Imponer una sola fila de salud por panel, unicidad de versión de transición por panel e integridad referencial sin eliminación en cascada del historial. Incorporar índices para alertas activas por panel/condición y parque mediante la relación actual, e historial por panel/fecha/versión. Definir longitudes, precisión UTC y almacenamiento de enums en los mapeos sin reutilizar ordinales incompatibles.

La unicidad de alerta activa debe imponerse en MariaDB además del control de aplicación. Elegir el índice/clave de condición activa según D5 y las capacidades de MariaDB 11.8: no proponer un índice filtrado de SQL Server ni una unicidad que impida conservar episodios cerrados de la misma condición.

### INF2. Confirmación consistente y concurrencia

En una transacción, serializar o validar mediante versión la evaluación del mismo panel, releer el estado/evidencia necesarios, aplicar las reglas de dominio y guardar salud, transición y apertura/cierre de alertas. Incluir el caso de creación concurrente de la primera fila. La combinación de transacción, claves únicas y reintento acotado debe producir un único cambio lógico ante dos instancias.

Los errores no deben dejar salud actual modificada sin su transición o alerta correspondiente. Mantener orden de bloqueo consistente y no sostener transacciones durante consultas al inversor ni envíos SignalR. Si la evidencia cambió durante el cálculo, volver a evaluar; no confirmar un resultado desactualizado.

La recuperación de evaluación pendiente debe estar respaldada por persistencia/reconciliación. La recuperación del hueco entre commit y publicación y el receptor por instancia se conectan al acuerdo D6; cualquier cambio técnico necesario se documenta antes de implementarse, sin crear un transporte paralelo.

### INF3. Lectura y simulador

Obtener evidencia mediante consultas acotadas por panel y fecha, preservando paneles sin lecturas y sin cargar el histórico completo ni consultar individualmente por cada panel. Obtener salud/alertas de forma coherente para la pantalla y paginar transiciones en el servidor. Las lecturas para interfaz no escriben ni activan evaluación incidental.

Reutilizar las fallas configurables de `InversorSimulador` y sus contratos actuales para la comprobación funcional. Si la evidencia aprobada en D1/D2 no existe en esos contratos, registrar la carencia y acordar el ajuste mínimo; no inferir información de operación inexistente ni cambiar el comportamiento histórico silenciosamente.

## 6. Presentation

### PRE1. Monitoreo, alertas e historial

Extender `Monitoreo.razor` con indicador visual y etiqueta textual de salud por panel y nodo. Diferenciar salud, estado administrativo, disponibilidad y potencia. Mostrar fecha UTC y evidencia insuficiente/vencida sin presentar el último estado conocido como actual. Los colores complementan el texto; no son el único indicador.

Incluir lista Radzen de alertas activas del parque seleccionado con panel, nodo/parque actuales, condición y detección UTC. Agregar una acción de consulta por panel que abra el historial paginado dentro del monitoreo o en un detalle accesible desde allí. No añadir formularios de alta/edición/borrado ni botones de reconocimiento/cierre manual.

### PRE2. Tiempo real y permisos

Reutilizar `/hubs/monitoreo`, su autorización y suscripción validada por parque. El adaptador de Presentation implementa `INotificadorSaludConfirmada` con el mecanismo D6 y `IHubContext`; Infrastructure no conoce el hub. No convertir las operaciones internas de persistencia en métodos del hub.

Tras cargar/suscribirse o reconectar, consultar salud, alertas e historial abierto para recuperar el estado confirmado. Gestionar avisos concurrentes durante carga, versiones repetidas, respuestas tardías y cambios de parque; liberar handlers, cancelaciones y recursos al abandonar la vista. Un cambio de salud no añade puntos falsos al gráfico de potencia existente.

Mantener acceso autenticado para los tres roles tanto en componentes como en servicios y hub. Rechazar anónimos y roles ajenos; mostrar estados de carga, vacío, error y reconexión. Mostrar fallas y recuperación automáticamente, sujeto al intervalo de adquisición/evaluación/distribución configurado, sin prometer una latencia fija todavía no acordada.

### PRE3. Composición y configuración

Registrar servicios/repositorios y ejecución periódica en el punto de composición. Externalizar los parámetros aprobados en D1/D2/D5 y el intervalo de evaluación, validar coherencia al arranque y no guardar secretos. No actualizar versiones de paquetes ni crear otro recolector. Aplicar migraciones mediante el procedimiento de despliegue existente, verificando compatibilidad durante reemplazo de instancias según D6.

## 7. Verificación y evidencias

| Id | Verificación requerida |
|---|---|
| V1 | Ejecutar las pruebas de dominio DOM3 y las existentes; cada regla nueva se implementa junto con sus pruebas. |
| V2 | Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` después de cambios de código y corregir la implementación; no alterar guardarraíles. No crear pruebas unitarias nuevas fuera de Domain. |
| V3 | Integración real MariaDB: migrar una base representativa con datos previos, comprobar conservación de valores/ordinales, relaciones, índices y ausencia de historial retrospectivo inventado; consultar salud inicial, alertas e historial paginado. |
| V4 | Integración: primera evaluación, condición sostenida, transición, recuperación y recurrencia D5; mismo lote repetido, concurrencia de dos instancias y fallo transaccional. Comprobar una alerta activa por condición y ninguna transición duplicada. |
| V5 | Integración: ausencia de eventos hasta desconexión, reinicio del evaluador, fallo entre guardado de lectura y evaluación, fallo de publicación y recuperación; evidencia atrasada, recuperación posterior y traslados según D4. No duplicar adquisición ni modificar lecturas históricas. |
| V6 | Funcional en navegador: simular falla y recuperación con el inversor existente; verificar panel, nodo, lista de alertas e historial sin recarga. Medir el tiempo observado y registrar intervalos configurados. Verificar cobertura insuficiente, cambio de parque, reconexión y liberación de recursos. |
| V7 | Seguridad: tres roles lectores, rechazo de anónimos/roles ajenos en consultas/hub e inexistencia de escritura manual accesible. |
| V8 | Dos instancias detrás de Nginx según D6: clientes confirmados en instancias diferentes reciben cambios y cierres; evaluaciones concurrentes no duplican transiciones/alertas; reemplazo de una instancia recupera el estado vigente. No dar por aprobado con grupos locales aislados. |

Usar los proyectos y herramientas de verificación existentes. Si faltan decisiones, entorno, mecanismo compartido o acceso al navegador, registrar el bloqueo y las comprobaciones no ejecutadas; no marcar esas verificaciones como aprobadas. Esta generación documental no implementa código ni ejecuta migraciones sobre la base habitual.

## 8. Cierre y documentación

Al implementar, crear `implementations.md` en esta carpeta con acuerdos D1–D6, comportamientos finales, archivos tocados, migración, configuración externa, pasos reproducibles de falla/recuperación/historial, coordinación de dos instancias y resultados V1–V8. Distinguir evidencia ejecutada de verificaciones omitidas y sus causas. Mantener requisitos, especificación y tareas coherentes con el alcance aprobado.
