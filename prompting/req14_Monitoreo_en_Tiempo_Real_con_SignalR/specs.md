# Especificaciones técnicas: Monitoreo en tiempo real con SignalR

## 1. Alcance y contexto

Fuente funcional: `requirements.md` de esta carpeta. Se incorpora una pantalla de monitoreo de un parque seleccionado, sus nodos y paneles, con estado inicial persistido, actualizaciones mediante un hub SignalR y una curva sencilla de potencia del parque en Radzen. Se incluyen autenticación, autorización, reconexión y distribución efectiva con Nginx y dos instancias.

Se mantienen .NET 8, Blazor Server, Radzen.Blazor existente, EF Core 8, Pomelo.EntityFrameworkCore.MySql 8.0.3, MariaDB 11.8 LTS y Keycloak. No se incorporan MediatR, proveedores alternativos ni dependencias o servicios no aprobados. Application solo depende de Domain; los componentes Blazor no acceden a Infrastructure.

### Base inicial reutilizada (antes de req14)

- `ParqueSolar`, `Nodo`, `Panel` y `LecturaPanel` ya existen. En el código actual, la lectura conserva `PanelId`, `NodoId` histórico, `FechaHora` UTC, `PotenciaGeneradaW` y `EstadoSalud`.
- La recolección existente usa `RecoleccionLecturasService`, `ISesionRecoleccionLecturas` y `RecoleccionLecturasWorker`; el guardado precede al retorno del ciclo y se coordina mediante MariaDB. No tiene actualmente un contrato de notificación de lecturas confirmadas.
- La recolección del simulador identifica mediciones por intervalos UTC configurables. Esto no define por sí solo qué lecturas se consideran vigentes en monitoreo.
- `IUsuarioActual` y los servicios existentes verifican autenticación y roles en Application. Presentation utiliza OpenID Connect y componentes Blazor interactivos; la navegación todavía no incluye monitoreo.
- La conexión SignalR interna de Blazor no sustituye al hub de notificaciones exigido. No se encontró una implementación de ese hub ni una decisión documentada de distribución de eventos entre instancias para este alcance.

Quedan fuera de alcance CRUD de topología, escritura de lecturas desde monitoreo, otra recolección, consulta directa de inversores, alertas, exportaciones e informes históricos avanzados. El histórico existente no se reemplaza.

## 2. Decisiones y acuerdos de implementación

| Id | Decisión pendiente | Condición de resolución |
|---|---|---|
| D1 | Definir vigencia temporal: cómo se alinean las últimas lecturas de distintos paneles, tolerancia de antigüedad/desfase y significado de un total cuando faltan muestras compatibles. Definir también cuándo una muestra deja de ser vigente aunque no lleguen eventos nuevos. | Acordar una política única y sus parámetros; no asumir que la última lectura de cada panel siempre puede sumarse. |
| D2 | Acordar con la tarea de alta disponibilidad de Luciano el mecanismo efectivo de distribución entre instancias y su comportamiento ante fallos, reinicios y notificaciones perdidas. | Registrar tecnología, dependencias/versiones autorizadas, responsables de configuración y recuperación. WebSockets, grupos locales y afinidad no resuelven esta distribución. No elegir unilateralmente Redis, un broker, un servicio administrado, sondeo compartido u otra infraestructura. |
| D3 | Fijar el máximo de puntos de la curva y cómo representar un agregado cuando las lecturas de sus paneles tienen distintos instantes admitidos por D1. Definir si el estado inicial aporta solo el punto vigente o también una ventana acotada de puntos persistidos. | El punto inicial vigente basta para iniciar la curva; cualquier ventana adicional requiere decisión explícita. Determinar fecha de referencia y tratamiento visible de cobertura incompleta, sin inventar un instante común. |
| D4 | Confirmar el cliente de conexión al hub desde el navegador y la versión concreta compatible con .NET 8; el proyecto no declara aún una biblioteca cliente para este hub. | Acordar el cliente SignalR y su distribución local/versionada conforme a las tecnologías autorizadas. No instalar paquetes ni depender de una CDN sin aprobación. La pantalla debe recibir efectivamente eventos del hub propio. |
| D5 | Definir si el monitoreo agrupa por topología actual o por nodo histórico de la lectura cuando un panel cambia de nodo, y qué muestras siguen siendo aplicables tras ese cambio. | Mantener la pertenencia a los nodos del parque seleccionado y la coherencia temporal, sin modificar el histórico ni contar el panel en dos nodos. |

Estas decisiones bloquean únicamente los cambios dependientes. Deben actualizarse estas specs y las tareas antes de cerrar contratos, fórmulas o configuración afectados. El diseño de distribución y su entrega operativa no se consideran terminados hasta resolver D2 y verificar dos instancias.

### Acuerdos del usuario del 29/10/2026

- D1 confirmado: usar el intervalo UTC más reciente entre muestras disponibles que aún estén vigentes, y sumar exclusivamente paneles de ese mismo intervalo. Una lectura vence cuando su antigüedad alcanza dos intervalos de recolección; `IntervalosVigencia` es configurable. Excluir del total las muestras incompatibles y mostrar cobertura parcial. Sin muestras elegibles, el total es desconocido, no cero. Evaluar el vencimiento sobre el snapshot ya cargado, sin sondeo adicional de MariaDB para distribuir novedades.
- D3 confirmado: hasta 120 puntos configurables, iniciando con el estado vigente, sin consultar una ventana histórica adicional. Usar el límite UTC del intervalo como fecha del punto. Si otro panel completa ese intervalo, actualizar el punto existente; conservar su indicador de cobertura parcial. Tras un hueco de intervalos, iniciar un tramo nuevo para no unirlo visualmente con el anterior.
- D4 confirmado: cliente oficial SignalR 8 servido localmente. Se fijó `@microsoft/signalr` 8.0.17 del registro npm oficial, conservando licencia y avisos; no se agregó una CDN ni otro framework.
- D5 confirmado: topología actual; una medición cuyo nodo histórico no coincide con el nodo actual del panel se identifica y no contribuye al total. No se modifica esa lectura.
- D2 aprobado: el usuario confirmó posteriormente que Luciano aprobó la propuesta y autorizó completar las tareas pendientes. Implementar avisos transaccionales y un cursor por instancia en la MariaDB existente, sin Redis ni otros servicios, según `propuesta_distribucion_mariadb.md`. Se autorizaron además Playwright y un navegador aislado exclusivamente para las verificaciones de pantalla.

## 3. Domain

### DOM1. Cálculo y vigencia

No se requieren nuevas entidades persistentes ni cambios de atributos, relaciones o estados de las entidades existentes. Incorporar una calculadora de dominio `GeneracionActualService`, independiente de EF Core, Blazor y SignalR, para aplicar la política aprobada en D1/D5 a un conjunto de paneles y lecturas.

El cálculo selecciona como máximo una lectura válida por panel, respetando el orden de `FechaHora`, aplica el criterio de compatibilidad temporal y suma cada panel una sola vez en su nodo y parque. La pertenencia utilizada se determina por D5. Todos los cálculos se expresan en W usando `decimal`; no se integra energía ni se almacenan totales de nodo/parque.

El resultado diferencia ausencia de lectura, lectura fuera de vigencia y lectura válida con potencia cero. Los agregados deben acompañarse de su cobertura y fechas de origen; la fórmula y representación de totales incompletos se cierran con D1. No representar automáticamente como cero lo desconocido ni presentar un agregado incompleto como completo.

Las actualizaciones con fecha menor a la ya aceptada no sustituyen el valor vigente. Una repetición del mismo panel/instante no añade otra contribución. No alterar la salud ni la fecha guardadas. La política recibe una referencia UTC explícita para poder evaluar antigüedad de forma consistente y reproducible.

## 4. Application

### APP1. Consulta autorizada del estado

Crear `IMonitoreoService`/`MonitoreoService` e `IMonitoreoRepository`. El servicio lista los parques consultables y obtiene el estado del parque solicitado, validando identidad autenticada y los roles `Administrador`, `Tecnico` o `Consulta` mediante `IUsuarioActual`, antes del acceso a datos. Validar identificador y existencia del parque; un parque existente sin nodos, paneles o lecturas devuelve una respuesta vacía identificada, no una lectura fabricada.

Usar DTO de lectura: `EstadoMonitoreoDto`, `NodoMonitoreoDto`, `PanelMonitoreoDto` y `PuntoGeneracionDto`. Transportan identificadores/nombres necesarios, potencia nullable en W, fechas UTC de las muestras, referencia de evaluación UTC, cobertura y situación de disponibilidad determinadas por DOM1. Las etiquetas pueden derivarse de esos estados en la vista; no recalcular totales ni vigencia en Razor o JavaScript. La definición final de cobertura y fecha del punto depende de D1/D3.

El servicio delega el cálculo a `GeneracionActualService` y entrega el modelo listo para representar. Propagar cancelación. Ninguna operación de monitoreo escribe datos, controla inversores o crea equipos. No acoplar estos servicios a `Hub`, `IHubContext`, circuitos Blazor ni usuarios implícitos de un worker singleton.

`DatosMonitoreoDto` transporta el snapshot interno de topología y candidatas; `ParqueMonitoreoDto` identifica opciones. `AccesoMonitoreoService` verifica la suscripción usando la identidad autenticada capturada por el hub, sin depender del proveedor de autenticación de circuitos Blazor. `MonitoreoService.ReevaluarAsync` aplica vigencia sobre sus datos cargados y vuelve a autorizar sin reconsultar MariaDB. `SerieMonitoreoService` conserva, ordena y acota los puntos dentro del ámbito del circuito; la vista no aplica esas reglas.

### APP2. Integración de lecturas confirmadas

Definir `INotificadorLecturasConfirmadas` como puerto de salida de Application. El aviso identifica los parques afectados y las mediciones mediante panel/instante UTC; no transporta secretos ni necesita distribuir indiscriminadamente los datos de otros parques. La forma de versión/cursor adicional, si D2 la requiere, se cierra con ese acuerdo y no supone una nueva entidad persistente por defecto.

Integrar el aviso en la recolección existente solo después del guardado confirmado. Para un lote fallido o revertido no se emite un aviso de éxito. Un fallo de publicación posterior al commit se registra como tal y no transforma el guardado confirmado en un fallo de persistencia ni provoca otra adquisición de mediciones.

La integración local se realiza mediante las identidades confirmadas devueltas en `ResultadoRecoleccionDto`, después de `GuardarAsync`, y el worker invoca `INotificadorLecturasConfirmadas`. Su registro de errores diferencia publicación y persistencia. El adaptador local `NotificadorLecturasConfirmadas` entrega una invalidación por parque; el aviso durable se conserva además dentro de la transacción de guardado para que la recuperación no dependa de este envío inmediato.

El aviso es una invalidación para consultar el estado persistido, no la fuente de verdad. Si llega repetido o fuera de orden, la consulta y DOM1 impiden retrocesos y doble contabilización. La forma de recuperar el hueco entre commit y publicación, y entre suscripción y consulta inicial, depende del mecanismo acordado en D2; debe quedar resuelta y verificada. No prometer entrega exactamente una vez ni añadir una tabla outbox o un bus por suposición.

### APP3. Concurrencia y recuperación de la vista

La carga inicial debe poder consultar MariaDB sin esperar un nuevo evento. Al conectar o reconectar se establece la suscripción autorizada del parque y se recupera el estado vigente; reconciliar avisos recibidos durante la carga para no perder una actualización entre ambos pasos.

Una respuesta tardía de una consulta anterior o de un parque abandonado no reemplaza el parque activo ni un estado más reciente. Se pueden agrupar invalidaciones mientras hay una consulta en curso, garantizando una nueva lectura del estado si llegaron cambios durante ella. Se conserva la identificación temporal por panel y la coherencia del agregado del mismo estado consultado.

La expiración de vigencia definida en D1 exige actualizar la evaluación aunque no lleguen nuevas mediciones. Cualquier temporización para esa reevaluación llama al caso de uso y no a los inversores; no es otra recolección. No crear sondeo adicional de persistencia como sustituto unilateral del acuerdo de D2.

## 5. Infrastructure

### INF1. Obtención eficiente desde MariaDB

Implementar `MonitoreoRepository` con EF Core y el contexto existente, detrás de `IMonitoreoRepository`. Obtener topología y últimas lecturas candidatas del parque con consultas de solo lectura y proyecciones acotadas; evitar cargar todo el histórico o hacer una consulta por panel. El cálculo final pertenece a Domain/Application.

Reutilizar índices de lecturas por panel/fecha y nodo/fecha y los vínculos existentes. La selección respeta D5 y permite distinguir equipos sin muestras de lecturas medidas a cero. La lectura del conjunto debe ser coherente para que paneles y agregados no mezclen resultados de diferentes respuestas; verificar la estrategia de consulta/snapshot sobre MariaDB sin inventar tablas de totales.

Para la curva se conservan solo los puntos acotados aprobados por D3; una ventana inicial adicional, si se acuerda, se consulta con límite explícito. La consulta de monitoreo no cambia el modelo solar. D2 aprobado incorpora persistencia técnica mediante la migración AddDistribucionMonitoreo descrita en INF2.

### INF2. Distribución de avisos

Diseño D2 aprobado: `GuardarAsync` abre una transacción y actualiza una fila única en `SecuenciaMonitoreo`, manteniendo su bloqueo de escritura hasta el commit. Las lecturas y `AvisosMonitoreo` se guardan en esa misma transacción; un rollback revierte también la secuencia. El orden de bloqueos es exclusión de recolección, fila de secuencia y persistencia del lote. El aviso contiene las identidades confirmadas y parques afectados como JSON, con una secuencia `BIGINT` única.

`CursoresMonitoreo` conserva una posición por identificador externo estable de instancia. `IDistribucionMonitoreoRepository`/`DistribucionMonitoreoRepository` leen lotes ordenados y confirman progreso después de entregar cada aviso; las consultas de una instancia no consumen el cursor de otra. Estas tres tablas son almacenamiento técnico, no entidades nuevas del dominio solar. La migración incremental `AddDistribucionMonitoreo` agrega únicamente este esquema técnico.

`DistribucionMonitoreoWorker` es un consumidor alojado por instancia; usa alcances cortos y entrega mediante `INotificadorLecturasConfirmadas`/`IHubContext`. Si falla, no confirma ese aviso y reintenta; al reiniciar conserva su cursor. La entrega es al menos una vez: la repetición no duplica lecturas ni puntos lógicos. Conservar todos los avisos en esta primera versión, sin purga automática, evita huecos por retención; no eliminar manualmente eventos no confirmados.

Externalizar `DistribucionMonitoreo:InstanciaId` (único y estable, hasta 100 caracteres), `IntervaloSegundos` (1 por defecto) y `TamanoLote` (100 por defecto). El valor local `solaruy-local` sirve para una sola instancia y debe sustituirse por identificadores distintos en el despliegue doble. Validar configuración al arrancar. Este consumidor solo lee avisos: no adquiere mediciones ni consulta inversores.

La publicación atómica pertenece al repositorio de recolección en Infrastructure. El acceso al registro compartido usa el contrato de Application `IDistribucionMonitoreoRepository`, y la entrega final conserva `INotificadorLecturasConfirmadas` con un adaptador de Presentation. La recepción es independiente de las conexiones de usuarios y no usa sus credenciales para acceder a MariaDB. Infrastructure no referencia `IHubContext` ni Presentation.

Registrar el repositorio por ámbito y el receptor como servicio hospedado; abrir contextos y conexiones por operación, propagando cancelación y liberando recursos al detenerse. Ante fallos, reintentar desde el cursor confirmado sin repetir la adquisición. La solución debe mantener una sola recolección global, pero distribuir el cambio a todas las instancias que atienden clientes.

## 6. Presentation

### PRE1. Hub y entrega

Crear `MonitoreoHub` en Presentation, con una ruta explícita, por ejemplo `/hubs/monitoreo`, protegida con autenticación y los tres roles autorizados. El hub solo permite suscribirse/desuscribirse del parque consultable y entregar avisos; no expone métodos de escritura sobre entidades ni de consulta de inversores.

Validar en el servidor la autorización y existencia del parque antes de asignar la conexión a un grupo por `ParqueId`. El identificador de grupo no sustituye la autorización. La suscripción y los envíos no deben revelar avisos de otros parques por confiar en identificadores recibidos del cliente. Aplicar el mismo control a consultas y a nuevas suscripciones tras reconectar; una sesión que perdió autenticación no sigue operando como autorizada.

El adaptador de entrega en Presentation usa `IHubContext<MonitoreoHub>` para notificar a los grupos locales al recibir un aviso compartido. El publicador de Infrastructure no referencia Presentation. Registrar contratos y adaptadores por constructor en el punto de composición, sin dependencias de Infrastructure en componentes Razor.

### PRE2. Pantalla y conexión

Crear la página `Monitoreo.razor` y una entrada de navegación para los tres roles. Usar Radzen para seleccionar parque y representar jerarquía parque/nodo/panel con identificadores legibles, potencia en W, cobertura y fechas claramente marcadas como UTC. Mostrar explícitamente sin datos, datos fuera de vigencia y cero medido. Diferenciar fecha de medición de hora de recepción; no etiquetar como reciente una muestra vieja.

El cliente acordado en D4 se conecta al hub propio y notifica al componente mediante un adaptador de presentación. El componente invoca `IMonitoreoService` para cargar/reconciliar el estado; no llama a repositorios ni hace sumas o decisiones de vigencia. Usar el contexto de renderizado de Blazor al actualizar la vista.

Mostrar estados de conexión, desconexión y reconexión sin presentar el último valor retenido como una nueva medición. Tras reconectar, recuperar suscripción y estado desde el servidor aun si la nueva conexión queda en otra instancia. Al cambiar de parque, retirar la suscripción anterior, cancelar o descartar cargas pendientes y aplicar solo respuestas del parque seleccionado. Liberar callbacks, conexión y recursos de interoperabilidad al abandonar la pantalla para no multiplicar suscripciones.

### PRE3. Gráfico en vivo

Usar un gráfico Radzen sencillo de potencia del parque frente a tiempo. Los puntos utilizan la fecha de medición/referencia aprobada en D3, nunca la hora de llegada de la notificación. Etiquetar UTC y W. El componente representa los puntos calculados, sin volver a agregar lecturas.

Conservar como máximo `MaximoPuntosGrafico`, parámetro externo positivo cuyo valor se acuerda en D3. Ordenar por fecha, deduplicar el mismo punto lógico y retirar los más antiguos al alcanzar el límite. Una invalidación repetida o una consulta de reconexión no agrega un punto ficticio; si varios paneles completan el mismo punto, actualizarlo. Los datos ausentes/incompatibles se representan según D1/D3 sin unir silenciosamente huecos ni sustituirlos por cero.

### PRE4. Configuración y despliegue

Registrar el hub, servicios de consulta, calculadora y adaptadores en `Program.cs`. Externalizar máximo de puntos y parámetros aprobados de vigencia/distribución/cliente; validar valores al arrancar y no versionar credenciales. No modificar el intervalo ni las reglas de adquisición de req011 por esta funcionalidad.

Coordinar con Luciano la configuración efectiva de Nginx para las conexiones de Blazor y del nuevo hub: rutas, negociación, WebSocket, afinidad y comportamiento durante el reemplazo de una instancia. No crear un despliegue paralelo a su tarea ni presentar una configuración propuesta como validada sin ejecutarla. El mecanismo D2 es obligatorio además de la afinidad.

Con el acuerdo D2 confirmado, incluir `deploy/nginx/monitoreo.conf.template` y su Compose para el proxy. Distribuir clientes con cookie de afinidad, admitir WebSocket y negociar HTTP, desactivar buffering y usar la otra instancia como respaldo ante fallo. Compartir claves de Data Protection y nombre de aplicación `SolarUY` mediante `DataProtection:RutaClaves` externa; configurar únicamente la dirección de proxy confiable en `ReverseProxy:Direccion` para encabezados reenviados. Los certificados y las claves se mantienen fuera del repositorio.

El circuito Blazor no se traslada entre procesos. El arranque del cliente reintenta recuperarlo y, si el servidor de respaldo no lo conoce, recrea la página de monitoreo automáticamente. Conservar el parque seleccionado en el parámetro de URL `parque` y volver a consultar el estado persistido tras recargar. En otras páginas, una conexión irrecuperable conserva la decisión de recarga manual para evitar perder entradas del usuario.

## 7. Verificación prevista para la implementación

Estas pruebas pertenecen a la futura implementación; generar estas specs no implica ejecutarlas.

- V1. Crear pruebas xUnit de Domain junto con `GeneracionActualService`: panel repetido, muestra atrasada, cero medido, ausencia de datos, expiración y compatibilidad temporal D1, pertenencia D5 y sumas sin duplicados. Verificar casos de cobertura incompleta y fechas resultantes aprobadas.
- V2. Mantener en verde `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` y `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj`. No alterar ni relajar las pruebas arquitectónicas. No crear pruebas unitarias de Application, Infrastructure o Presentation; comprobar sus comportamientos mediante integración o pruebas funcionales.
- V3. En integración con MariaDB, verificar estado inicial para parques con datos y sin datos, consulta acotada de últimas lecturas, coherencia de paneles/agregados, unidades y uso de índices. Verificar la conservación del histórico y que monitoreo no consulta inversores ni escribe entidades.
- V4. Persistir lecturas mediante el circuito existente y comprobar avisos posteriores al commit, ausencia de avisos de éxito ante rollback y recuperación del hueco de publicación según D2. Provocar fallos del transporte sin duplicar la adquisición ni perder permanentemente el estado vigente.
- V5. En la pantalla, verificar carga inicial, actualización automática de valores y gráfico, puntos acotados, orden temporal, repetición/atraso de eventos, cambios rápidos de parque, respuestas tardías y liberación de suscripciones. Verificar reevaluación al expirar la vigencia sin nuevas mediciones.
- V6. Comprobar acceso de los tres roles en página, consultas y hub; rechazar anónimos y roles ajenos y validar suscripciones del lado del servidor. Confirmar que no hay operaciones de escritura expuestas.
- V7. Probar desconexión/reconexión de cliente y del transporte, recuperación de suscripción/estado y cierre o expiración de sesión. Incluir una escritura concurrente con la carga inicial para comprobar que la ventana de suscripción no pierde cambios.
- V8. Ejecutar con Nginx, dos instancias y clientes conectados a cada una, verificando explícitamente qué instancia atiende a cada cliente. Persistir desde cualquiera de las instancias mediante el circuito autorizado, sin habilitar dos recolectores globales, y comprobar recepción en ambas. Reemplazar una instancia y reconectar contra la otra, recuperando valores de MariaDB. Registrar mecanismo D2 y configuración utilizados.

No considerar V8 aprobado por pruebas solo en memoria o con clientes conectados al mismo servidor. Si no se dispone del despliegue o de la decisión D2, registrar el bloqueo y la comprobación no ejecutada.

## 8. Cierre y documentación

Al terminar la implementación, crear `implementations.md` en esta carpeta con cambios realizados, archivos tocados y resultados de pruebas. Incluir decisiones D1–D5 resueltas, responsabilidades coordinadas con Luciano, configuración externa, pasos de ejecución del monitoreo y reproducción de pruebas de dos instancias, reconexión y datos atrasados. Registrar expresamente pruebas operativas no ejecutadas y su causa. No crear ese informe como si la implementación ya se hubiera realizado al generar estas specs.
