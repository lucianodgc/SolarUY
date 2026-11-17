# Especificaciones: REQ017 — Chatbot en lenguaje natural (solo consultas)

## C0. Fuente y alcance

Fuente funcional: [requirements.md](requirements.md), Entrega 3. Este documento define las decisiones técnicas; el estado verificado y sus limitaciones se registran en [tasks.md](tasks.md) e [implementations.md](implementations.md).

El usuario autenticado consulta parques, equipos, generación diaria, paneles con fallas, mantenimientos pendientes e histórico mediante un panel Blazor/Radzen. Application coordina y autoriza; Infrastructure integra Microsoft Agent Framework, Ollama local con `qwen3:8b` y el cliente/servidor MCP de SolarUY. OpenAI permanece como proveedor opcional configurable. El asistente no puede crear, modificar ni eliminar información ni ejecutar órdenes sobre equipos, aun cuando el usuario sea administrador.

Conservar .NET 8, EF Core 8, Pomelo 8.0.3, MariaDB 11.8, Keycloak/OpenID Connect y Blazor Server con Radzen.Blazor 6.0.0 instalado. No incorporar MediatR, otro ORM, una base vectorial, agentes múltiples ni trabajos de recolección nuevos. Implementar un servidor MCP de SolarUY y consumir sus herramientas mediante Microsoft Agent Framework. Las consultas deben recorrer el protocolo MCP; no basta con registrar funciones locales que lo omitan.

El modelo existente aporta la capacidad de conversación; este requisito no incluye entrenamiento de modelos. Antigravity sirve como referencia de cliente compatible con MCP. Su instalación, acceso externo e integración no son entregables: el panel exigido sigue dentro de SolarUY. MCP conecta herramientas y datos; no determina el proveedor de IA.

Se consultaron las reglas de `.agents/rules/`, las especificaciones de mantenimiento y autorización, y los contratos y servicios vigentes. Las referencias de partida son:

| Componente existente | Uso y precaución |
| --- | --- |
| `IUsuarioActual` / `UsuarioActualService` | Identidad y roles del circuito Blazor; nunca identidad suministrada por el modelo. |
| `IMonitoreoService.ListarParquesAsync` | Descubrimiento autorizado de parques. `ConsultarAsync` calcula sobre lecturas guardadas y no sustituye la telemetría en vivo. |
| `IInversorInventarioRepository.ListarAsync` | Inventario de nodos/paneles y pertenencia al parque, sin sincronizar el simulador. |
| `IInversorClient` y `PanelInversorEstadoDto` | Reutilizar exclusivamente operaciones de obtención de estado. No registrar esta interfaz completa como herramienta. |
| `IHistoricoGeneracionService` | Generación diaria e histórica con períodos, unidades, cobertura y procedencia existentes. |
| `ITareaMantenimientoService.ListarAsync` | Consulta de tareas con los permisos existentes: administrador, o técnico limitado a sus tareas. |
| `SolarUY.Integration.Tests` y `CodeQuality.Tests` | Escenarios integrados y guardarraíles existentes. |

`InversorService.ConsultarAsync` y `TelemetriaEnVivoService.ConsultarAsync` pueden ejecutar `SincronizarInventarioAsync`. El flujo del chat no debe invocarlos tal como están: una consulta no debe crear o actualizar inventario del simulador. El adaptador de lectura de I2 evita esa sincronización sin cambiar el funcionamiento del monitoreo ni del recolector.

### C1. Decisiones confirmadas para la implementación

El usuario sustituyó la elección inicial de OpenAI por **Ollama local con `qwen3:8b`**, conservando Microsoft Agent Framework y el transporte MCP interno. Ollama es el predeterminado y no necesita saldo, cuenta ni clave de OpenAI. La [ficha oficial de qwen3:8b](https://ollama.com/library/qwen3:8b) declara soporte de herramientas. En el entorno se encontró Ollama 0.40.16 con el modelo `500a1f067a9f` (Q4_K_M, aproximadamente 5,2 GB); las pruebas reales deben registrar sus resultados y limitaciones de hardware.

Implementar `OllamaChatClient : IChatClient` en Infrastructure mediante `HttpClient` y `System.Text.Json`, usando la API nativa local `/api/chat`: mensajes, esquemas de herramientas, llamadas y resultados de función. Microsoft Agent Framework conserva el ciclo de invocación. Enviar `stream=false`, `think=false` y un contexto configurable de 16384 tokens para incluir el catálogo y los resultados sin mostrar razonamiento interno. La reducción provisional a 8192 permitió inferencias básicas, pero recortó el resumen de dos parques, cuyo prompt completo ocupó 12655 tokens. Se conserva 16384 con los ajustes de memoria siguientes y se documentan los recursos necesarios. No requiere paquetes adicionales al stack ya fijado; no cambiar el modelo ni descargar otro automáticamente. El endpoint predeterminado es `http://127.0.0.1:11434/` y solo se admiten direcciones loopback sin credenciales, consulta ni redirecciones.

Para reducir la memoria de ejecución sin recortar contexto ni herramientas, el adaptador envía `num_batch=128`, `use_mmap=true` y `keep_alive="60s"`. El lote reduce los búferes de cálculo, el mapeo evita la carga anónima predeterminada observada en Windows/CUDA y la permanencia acota la residencia tras inactividad. No se cambian pesos, cuantización, modelo, parámetros de muestreo ni catálogo MCP. Estos campos están admitidos por la [API de Ollama 0.40.16](https://github.com/ollama/ollama/blob/v0.40.16/api/types.go); la [documentación de permanencia](https://docs.ollama.com/faq#how-do-i-keep-a-model-loaded-in-memory-or-make-it-unload-immediately) describe su liberación. El resultado bajo presión de RAM y los servicios temporalmente detenidos deben registrarse; estos ajustes no garantizan capacidad si otras aplicaciones agotan la memoria del sistema.

Conservar OpenAI mediante Responses API como alternativa explícita (`Chatbot:Proveedor=OpenAI`, modelo `gpt-6-luna` y clave externa). Su endpoint es el oficial del SDK. La [documentación de GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna.md) documenta function calling mediante Responses API. La comprobación inicial devolvió `429 credit_balance_exhausted`; eso no debe impedir el uso de Ollama. No ejecutar pruebas pagas de OpenAI por defecto ni seleccionarlo automáticamente cuando falle el modelo local.

Dependencias fijadas: `Microsoft.Agents.AI` y `Microsoft.Agents.AI.OpenAI` 1.28.0 y `ModelContextProtocol.Core` 2.1.0. El conector resuelve OpenAI 2.14.0 y Microsoft.Extensions.AI 10.11.0. Sus paquetes incluyen soporte para `net8.0`; se verifican además compilación y pruebas sin cambiar .NET 8, EF Core, Pomelo ni Radzen. La documentación oficial de [herramientas MCP en Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/agents/tools/local-mcp-tools) describe la integración con el SDK MCP de C#.

El host es el backend existente de SolarUY. Crear un servidor/cliente por turno, ligado al ámbito del usuario autenticado, usando `StreamServerTransport` y `StreamClientTransport` sobre dos pipes en memoria. Se intercambian mensajes MCP reales de inicialización, descubrimiento e invocación. No se abre puerto, endpoint HTTP, proceso externo ni servicio compartido entre usuarios. La confianza del transporte procede de su construcción interna en el circuito autenticado: el propietario se captura desde `IUsuarioActual` y se verifica antes y después de cada llamada y antes del descubrimiento. Nunca se toma de argumentos JSON ni identificadores de sesión enviados por el modelo.

Solo la alternativa OpenAI utiliza la API Responses y el adaptador de almacenamiento remoto, marcados como experimentales por esos SDK. Aislar la aceptación de `OPENAI001` y `MAAI001` únicamente en `OpenAiModeloChatbotFactory`, conservando el resto de los diagnósticos y `store=false`. El adaptador local usa las APIs estables de .NET 8. No se requiere un nuevo cliente Keycloak ni cambios de Nginx. Una exposición HTTP futura de MCP necesitaría definir su propia autenticación y destinatario de tokens; queda fuera de esta entrega.

## 1. Domain

### D1. Reutilización del modelo

Reutilizar `ParqueSolar`, `Nodo`, `Inversor`, `Panel`, `LecturaPanel`, `TareaMantenimiento` y sus estados vigentes. El chat no incorpora entidades, atributos, relaciones, migraciones ni comportamiento nuevo de dominio. Sus mensajes y resultados son DTO de Application, sin persistencia de conversaciones.

Conservar los estados diferenciados de las fuentes: `EstadoPanel.Falla` representa el registro del panel; `EstadoSaludPanel.Fallado` describe la respuesta actual del inversor. Una lectura histórica no prueba una falla actual. Mantener origen e instante de los datos para explicar discrepancias, sin cambiar estados por consultarlos.

### D2. Magnitudes y períodos

La potencia instantánea se informa en W según la telemetría existente; la energía acumulada se obtiene del histórico en kWh. No calcular energía diaria sumando potencias ni estimarla a partir de una muestra. Reutilizar sin alterar las reglas de integración, cobertura, huecos y procedencia histórica por nodo; conservar el diagnóstico `LecturasSinNodoHistorico` con su alcance original.

## 2. Application

### A1. Contratos y coordinación

Crear el módulo `SolarUY.Application.Chatbot` con interfaces y DTO propios, independientes de Agent Framework, SDK del proveedor, SDK MCP, EF Core, HTTP, Blazor y Radzen. Inyectar dependencias por constructor y propagar `CancellationToken`.

- `IChatbotService.EnviarPreguntaAsync(PreguntaChatDto, CancellationToken)` y `ChatbotService`: validar la pregunta no vacía, autenticar, coordinar el turno con el cliente y devolver `RespuestaChatDto`.
- `IChatbotClient`: puerto implementado en Infrastructure para procesar el turno con el modelo y consumir las herramientas por un cliente MCP. Recibe mensajes y devuelve un resultado neutral, sin tipos del SDK. La autenticación del salto MCP se resuelve fuera de los mensajes/argumentos del modelo según C1 y A3; no recibe delegados que permitan omitir MCP.
- `IConsultaChatbotService` y `ConsultaChatbotService`: operaciones explícitas de A2 y autorización de A3. Los adaptadores del servidor MCP delegan aquí, incluso ante invocaciones directas de herramientas desde otro cliente autenticado.
- `IEstadoEquiposConsultaClient`: puerto de solo lectura de telemetría por nodos previamente autorizados, implementado según I2. No ofrece sincronización, activación, desactivación ni adquisición/guardado de lecturas.
- `PreguntaChatDto` y mensajes de conversación: texto y contexto conversacional de la sesión. `RespuestaChatDto`: texto y estado del turno, con resultados de consultas que indiquen origen, objetivo, período o instante, unidad y cobertura cuando correspondan.
- Resultados de funciones: distinguir éxito con datos, consulta vacía, objetivo inexistente, entrada inválida, acceso denegado y fuente no disponible. Una colección vacía exitosa es distinta de una consulta que no se pudo ejecutar. Cero mantenimientos pendientes y cero fallas con paneles efectivamente consultados son resultados `Correcto`, con mensajes específicos del parque. Sin paneles para evaluar fallas, sin estado o sin lecturas de generación se conserva `SinDatos`, identificando el apartado y objetivo en el aviso; no añadir una advertencia global de falta de datos por una colección vacía válida.

El contexto conversacional se mantiene solo en memoria del circuito/sesión y se aísla por usuario. No aceptar mensajes con rol de sistema/herramienta ni resultados de funciones suministrados por el navegador. El historial ayuda a interpretar la pregunta; nunca demuestra permisos ni vigencia de un dato. Al cambiar o perder la identidad, cancelar el turno y descartar el contexto anterior. No usar sesiones de conversación compartidas o un cliente singleton que capture identidad o historial.

Conservar en el servicio scoped los intercambios visibles completados, incluidos rechazos y errores controlados, además del contexto operativo que ya se enviaba al modelo. `IChatbotService.ObtenerConversacionAsync` devuelve una copia después de comprobar identidad/roles; ante pérdida o cambio de identidad descarta ambos historiales. Al volver a la página, restaurar esa conversación en lugar de reiniciarla. Destruir la vista cancela el turno activo, pero conserva los intercambios anteriores; el turno cancelado no se registra. Solo «Nueva conversación», cambio/pérdida de identidad o cierre del circuito limpian el estado. No agregar almacenamiento del navegador, persistencia SQL ni un historial compartido entre circuitos, aunque pertenezcan al mismo usuario.

`ChatbotService` reconoce mensajes completos de saludo, agradecimiento, despedida y ayuda mediante expresiones y patrones acotados normalizados y devuelve texto de cortesía definido en servidor, sin invocar IA ni herramientas. Distinguir «cómo estás» del saludo inicial y admitir preguntas generales de capacidades como «hola, qué podemos hacer hoy», con saludo inicial opcional. Los patrones se aplican al mensaje completo después de retirar únicamente ese saludo; una pregunta que incluya datos operativos no coincide solo por su comienzo. Conserva autenticación, revalidación de identidad y registro en el historial de la sesión. Solo estas respuestas locales no operativas pueden ser correctas sin consultas de respaldo. Un saludo que incluya una pregunta operativa continúa por IA/MCP; las órdenes siguen rechazándose antes de esta clasificación. No aceptar sin evidencia una respuesta arbitraria del modelo.

`PlanResumenChatbot` identifica la consulta general de ejemplo en Application y marca el turno con `ResumenDiario`, sin aceptar esa marca desde el navegador. Su plan devuelve las cuatro consultas por cada parque descubierto con autorización. Infrastructure ejecuta ese plan exclusivamente mediante las herramientas MCP y adjunta sus resultados como mensajes de herramientas del turno antes de pedir la redacción al modelo. Así el resumen general no depende de que el modelo recuerde solicitar todos los apartados; las preguntas específicas conservan la selección de herramientas por Agent Framework.

Para los demás turnos operativos, `PlanificacionChatbot` en Infrastructure usa el mismo modelo mediante Agent Framework con salida JSON de esquema cerrado (`accion`, `parques`), limitado a los últimos doce mensajes y sin herramientas ni credenciales. Admite seleccionar herramientas normalmente, listar parques, detallar parques, consultar sus equipos o pedir aclaración. El historial determina referencias y alcance, nunca datos actuales ni permisos. Validar acción, estructura y referencias contra un nuevo listado MCP autorizado; un nombre desconocido o ambiguo requiere aclaración. El detalle ejecuta las cuatro consultas de `PlanResumenChatbot` por cada parque seleccionado; equipos ejecuta el estado por cada parque. Todas las consultas pasan por MCP y las seis herramientas siguen siendo las únicas disponibles.

El listado MCP aporta la cantidad de parques autorizados, que puede incorporarse explícitamente a la respuesta. Un plan no válido se rechaza sin ejecutar consultas arbitrarias. `RequiereAclaracion` permite una pregunta de precisión definida en servidor sin afirmar datos; conservar ese intercambio en el contexto para interpretar la respuesta siguiente. Ante una respuesta sin invocaciones en el modo normal, permitir un solo intento correctivo dentro del mismo tiempo máximo; si sigue sin consultar, mantener el bloqueo de afirmaciones sin evidencia. Permitir que el modelo proponga varias llamadas, ejecutadas secuencialmente para respetar EF y los servicios existentes.

Ollama traduce `ChatOptions.ResponseFormat` a `format` de la API nativa para el plan estructurado, compatible con la versión instalada y con las [salidas estructuradas oficiales](https://docs.ollama.com/capabilities/structured-outputs). No se simula una opción `tool_choice` que esa API nativa no ofrece. La redacción conserva el agente con las funciones MCP, los resultados completos y el control de evidencia. El límite de contexto configurado sigue aplicando: no se promete una conversación ilimitada ni capacidad para cualquier cantidad de parques.

### A2. Catálogo cerrado de herramientas MCP

Publicar exclusivamente las siguientes operaciones en el servidor MCP. Los nombres son los contratos internos previstos; los nombres y esquemas MCP registrados ante el modelo deben corresponder inequívocamente a estas operaciones. El cliente MCP conserva una lista explícita de herramientas permitidas y valida el catálogo recibido antes de ofrecerlas al agente.

| Operación | Entrada y comportamiento |
| --- | --- |
| `ListarParquesAsync` | Sin identidad ni roles como parámetros. Delegar en la consulta autorizada existente para resolver nombres e identificadores. |
| `ConsultarEstadoActualAsync` | Parque identificado. Validar existencia/pertenencia de nodos y obtener telemetría actual mediante `IEstadoEquiposConsultaClient`; informar paneles, potencia, estado de equipos e instante real de consulta. |
| `ConsultarGeneracionDiariaAsync` | Parque y fecha local; para «hoy», resolverla en servidor con `TimeProvider` y `America/Montevideo`. Delegar en `IHistoricoGeneracionService.ConsultarAsync` con objetivo parque y `PeriodoHistorico.Diario`. |
| `ConsultarPanelesConFallaAsync` | Parque identificado. Consultar salud actual mediante el puerto de telemetría; se pueden incluir registros `EstadoPanel.Falla` del inventario, identificándolos como estado registrado. No inferir fallas a partir de potencia cero o ausencia de datos. |
| `ConsultarMantenimientosPendientesAsync` | Parque identificado. Consultar `ITareaMantenimientoService.ListarAsync` con `EstadoTareaMantenimiento.Pendiente`, conservar su alcance por responsable y limitar el resultado a paneles del parque mediante inventario de lectura. No incluir completadas, canceladas ni cambiar estados. |
| `ConsultarGeneracionHistoricaAsync` | Objetivo existente y período diario, mensual o anual. Construir `FiltroHistoricoDto` válido y delegar en el servicio histórico existente. Conservar objetivos parque/nodo/panel admitidos por ese servicio y validar las relaciones declaradas en la consulta. |

No exponer `CompararAsync`, exportación, herramientas genéricas de repositorio, SQL, comandos, búsqueda web ni funciones de escritura. El modelo no recibe repositorios, servicios de mantenimiento completos ni el cliente completo del inversor. Los DTO devueltos no necesitan indicadores de acciones de gestión como `PuedeEditar`.

Validar identificadores, períodos y tipos antes de consultar fuentes. Resolver nombres con datos autorizados; para referencias a nodos/paneles del histórico, consultar internamente `IHistoricoGeneracionService.ListarOpcionesAsync`, sin agregar otra herramienta al catálogo. Si hay ambigüedad, pedir al usuario que precise la selección. No inventar identificadores ni interpretar «panel 3» como un `Guid` o número de serie existente. Reutilizar las fechas locales `DateTimeKind.Unspecified`, límites UTC y fin exclusivo definidos por el histórico. La generación de «hoy» identifica su cobertura y no se presenta como un día completo cuando hay datos parciales.

### A3. Autenticación y autorización en cada función

`ChatbotService` y cada operación de `ConsultaChatbotService` obtienen el usuario mediante `IUsuarioActual.ObtenerAsync`. Exigir autenticación, identificador estable no vacío y un rol reconocido antes de acceder a una fuente. No confiar en un control previo de la pantalla, en instrucciones del modelo ni en parámetros de usuario/rol/responsable enviados por el chat.

El servidor MCP interno se crea únicamente desde el ámbito autenticado descrito en C1. Usa el `IUsuarioActual` real de ese ámbito, conserva el propietario al abrir el transporte y vuelve a comprobar identidad y roles antes/después de ejecutar cada consulta. Application también valida los permisos por operación. No crear otro `AuthenticationStateProvider` ni otro ámbito que pierda la identidad del circuito; Application continúa independiente de claims, HTTP y SDK MCP.

No aceptar usuario, roles o credenciales como argumentos de herramientas ni encabezados de identidad sin validación. No compartir una sesión/conexión autenticada MCP entre usuarios ni sustituirlos por una cuenta con permisos generales. El identificador de sesión MCP no acredita identidad: verificarla en cada invocación, incluso sin pasar por el panel. No enviar tokens de usuario o credenciales MCP al proveedor de IA ni reenviarlos a fuentes externas para otro destinatario.

| Consulta | Administrador | Tecnico | Consulta |
| --- | --- | --- | --- |
| Acceso al chat; parques, equipos, generación, fallas e histórico | Lectura conforme a los servicios existentes | Lectura conforme a los servicios existentes | Lectura conforme a los servicios existentes |
| Mantenimientos pendientes | Todas las tareas autorizadas del parque | Solo sus tareas del parque | Acceso denegado |
| Cualquier escritura u orden | Prohibida desde el chat | Prohibida desde el chat | Prohibida desde el chat |

Los permisos actuales de mantenimiento no conceden lectura al rol `Consulta`. El resumen de mantenimientos realizados del módulo de reportes tampoco autoriza consultar pendientes. No ampliar esa política por incorporar el chat. En usuarios con varios roles aplicar la política vigente del servicio de mantenimiento. Al denegar acceso, no enviar datos restringidos al modelo ni revelar su existencia, nombres, cantidades o responsables.

Revalidar el acceso en cada invocación y antes de devolver el resultado del turno. Los identificadores y filtros propuestos por la IA son entradas no confiables. Ninguna función permite sustituir al actor autenticado; los filtros por responsable los determina el servicio existente. La autorización se conserva aun llamando directamente a la función desde código servidor.

### A4. Respuestas fundamentadas y rechazo de acciones

La instrucción del asistente limita su cometido a consultas de SolarUY y exige basar afirmaciones operativas en resultados exitosos de funciones del turno. Nombres, cifras, estados, fechas y registros proceden de esos resultados; el historial y el conocimiento general del modelo no sustituyen la consulta. Mantener los resultados estructurados para contrastar la respuesta con su evidencia y descartar respuestas operativas sin consultas de respaldo.

Ante «¿Cómo está funcionando todo hoy?», descubrir los parques autorizados y consultar estado actual, generación diaria, fallas y mantenimientos pendientes donde haya permiso. Si el usuario ya especificó un parque, limitarse a él. Presentar el resultado por parque, separando potencia actual de energía diaria y explicando cobertura, fuentes fallidas o apartados no autorizados. No mostrar un total global completo si algún parque o fuente carece de datos. Si no existen parques disponibles, informarlo sin inventar un resumen.

Ante «Desactivá el panel 3», altas, modificaciones, borrados u otras órdenes, responder que el chatbot solo consulta y no ejecutar ninguna acción. La protección efectiva es el catálogo cerrado y sus implementaciones sin efectos de escritura, incluso si el modelo incumple las instrucciones o propone una función desconocida. No transformar una orden en una escritura realizada por otro servicio.

Tratar preguntas y textos procedentes de datos como contenido no confiable: no pueden cambiar instrucciones de sistema, permisos ni catálogo de herramientas. Una solicitud de ignorar restricciones, revelar claves, suplantar roles o ejecutar SQL se rechaza sin entregar secretos ni habilitar capacidades nuevas.

### A5. Errores y cancelación

Traducir fallas del proveedor, conexión/protocolo MCP y fuentes a estados y mensajes seguros. Informar datos insuficientes, objetivos inexistentes o ambigüedad; no confundir un acceso denegado o un fallo con «no hay fallas» o «no hay mantenimientos». Ante una falla parcial se pueden comunicar los datos obtenidos, señalando qué parte no pudo consultarse. Ante caída del modelo o MCP, informar indisponibilidad y permitir un nuevo envío sin bloquear otras pantallas. No sustituir una conexión MCP fallida por llamadas locales que omitan el protocolo o su autenticación.

Propagar cancelación y establecer tiempo máximo configurable para el turno del proveedor. No incorporar reintentos ilimitados ni resultados simulados como alternativa. Una respuesta cancelada o correspondiente a una identidad anterior no se incorpora al contexto visible.

## 3. Infrastructure

### I1. Cliente de IA y cliente MCP

Implementar `ChatbotClient : IChatbotClient` en `SolarUY.Infrastructure.Chatbot`, utilizando Microsoft Agent Framework y el SDK MCP aprobados en C1. `ProveedorModeloChatbotFactory` selecciona `OllamaChatClient` por defecto u `OpenAiModeloChatbotFactory` solo ante configuración explícita. Encapsular en Infrastructure la creación del agente, el cliente MCP, descubrimiento/invocación de herramientas, traducción de mensajes y configuración del proveedor. El cambio de proveedor no modifica casos de uso, autorización ni herramientas MCP.

Conectar únicamente al servidor MCP de SolarUY configurado en servidor, autenticar con el contexto del usuario y descubrir sus herramientas mediante MCP. Validar nombres y esquemas contra el catálogo de A2 y registrar solo las herramientas permitidas en el agente. No aceptar direcciones MCP propuestas por el modelo/usuario, ni incorporar automáticamente herramientas nuevas porque el servidor las anuncie. Rechazar herramientas desconocidas o esquemas incompatibles; si falta una herramienta esperada, informar la función no disponible sin inventar datos.

El flujo de una consulta es `Blazor → IChatbotService → IChatbotClient / Agent Framework → cliente MCP → servidor MCP de SolarUY → IConsultaChatbotService → fuentes autorizadas`. El cliente interactúa con la API del modelo para recibir propuestas de herramientas y redactar la respuesta con sus resultados. La invocación MCP se ejecuta desde el backend de SolarUY, conservando allí las credenciales; no delegar al proveedor la conexión autenticada al servidor MCP.

Application no depende de Infrastructure. El modelo solo propone argumentos funcionales y no puede elegir la identidad ni la dirección del servidor. Las herramientas recibidas de MCP no son delegados locales directos de los servicios de Application. Mantener cliente y sesiones MCP aislados por usuario, propagar cancelación/tiempo máximo y liberar recursos al finalizar su ámbito o perder la identidad.

### I2. Consulta de equipos sin sincronización ni órdenes

Implementar `EstadoEquiposConsultaClient : IEstadoEquiposConsultaClient` en Infrastructure como adaptador de las operaciones de lectura existentes de `IInversorClient`: `ObtenerEstadoPanelesAsync`, `ObtenerEstadoVolcadoRedAsync` y `ObtenerEstadoEncendidoAsync`. Recibir únicamente los nodos del parque que Application resolvió y autorizó mediante el inventario existente; no realizar descubrimiento libre dirigido por el modelo.

Registrar el instante de obtención con el reloj del servidor. Conservar salud, actividad y potencia reportadas por la fuente, sin inventar un estado de dominio o atribuir al nodo un indicador que la fuente entregue solo a nivel global del simulador. Si el simulador no conoce el inventario, indicar la indisponibilidad/inconsistencia; no corregirla sincronizando desde el chat. La recolección y sincronización existentes continúan bajo sus responsables actuales.

No invocar `SincronizarInventarioAsync`, `ObtenerMedicionesRegistradasAsync`, activación/desactivación, cambios de volcado o encendido, `SaveChanges` ni operaciones de escritura de repositorios durante un turno. El adaptador tampoco usa `InversorService.ConsultarAsync` ni `TelemetriaEnVivoService.ConsultarAsync` mientras mantengan la sincronización descrita en C0.

La generación se consulta por el servicio histórico existente y los pendientes por el servicio de mantenimiento. Reutilizar los repositorios de lectura actuales; este requisito no introduce tablas, migraciones ni SQL generado por el modelo. Evitar consultas concurrentes sobre una misma instancia de contexto EF o servicios con estado que no admitan concurrencia.

### I3. Configuración segura y composición

Configurar `Chatbot:Proveedor` (`Ollama` por defecto), `Chatbot:Modelo` (`qwen3:8b` por defecto; configurar explícitamente `gpt-6-luna` al elegir OpenAI), `Chatbot:TiempoMaximoSegundos` (300, rango técnico de 1 a 300), `Chatbot:Ollama:Endpoint` (`http://127.0.0.1:11434/`), `Chatbot:Ollama:Contexto` (16384, rango técnico de 4096 a 32768) y `Chatbot:Mcp:Transporte` (`Interno`). `Chatbot:OpenAI:ApiKey` es externo y solo se exige al seleccionar OpenAI. No hay dirección MCP ni credencial MCP estática: ambos extremos pertenecen al mismo proceso y al ámbito autenticado descrito en C1. Validar las opciones sin bloquear otras funcionalidades. Nunca habilitar consultas MCP anónimas ni cambiar a un proveedor pago al faltar Ollama.

Guardar la clave de OpenAI exclusivamente en `user-secrets` durante desarrollo y variables de entorno en despliegue (`Chatbot__OpenAI__ApiKey`). La identidad de Keycloak permanece en el circuito del backend; no se transmite por MCP ni al modelo y no se convierte en credencial estática compartida. Los archivos versionados solo contienen opciones no sensibles y documentación. No colocar claves en mensajes de sistema, herramientas, DTO, URL del navegador, JavaScript, respuestas ni registros de errores. Enviar únicamente información autorizada necesaria para responder.

Agregar registros de Infrastructure en su composición e interfaces/servicios de Application en `Program.cs`. La referencia a Infrastructure queda en el arranque. Mantener conversación y fábricas MCP scoped al circuito; cada turno abre y libera su par cliente/servidor en ese ámbito con comprobación del propietario. Los componentes `.razor` solo invocan Application. No modificar autenticación de otras rutas, workers, recolección ni despliegue para agregar el transporte interno.

### I4. Servidor MCP de SolarUY

Implementar en `SolarUY.Infrastructure.Chatbot` los adaptadores MCP del catálogo A2, usando el SDK aprobado. Cada herramienta define nombre, descripción, esquema de argumentos funcionales y resultado estructurado, y delega en la operación correspondiente de `IConsultaChatbotService` del ámbito autenticado. No duplicar cálculos, permisos ni acceso a datos dentro del adaptador. El registro en el host se realiza desde su composición conforme a I3, manteniendo ASP.NET y tipos de protocolo fuera de Application y Domain.

Publicar y procesar descubrimiento e invocación reales de herramientas mediante MCP. Admitir únicamente el catálogo cerrado, validar argumentos antes de delegar y mapear los estados neutrales de Application a respuestas MCP sin excepciones internas ni secretos. No publicar servicios completos por reflexión ni herramientas administrativas, de SQL, shell, escritura o ejecución arbitraria. La autenticación se verifica antes de devolver catálogo o datos y cada llamada mantiene los permisos de A3.

El servidor ofrece consultas, sin ejecutar un agente o llamar al modelo de IA por su cuenta. Su implementación y la del cliente no requieren entrenar modelos ni integrar Antigravity. Una comprobación con otro cliente MCP controlado puede validar llamadas directas, sin convertir el acceso desde productos externos en un entregable de esta etapa.

## 4. Presentation

### P1. Panel de conversación

Crear `Chatbot.razor` con ruta `/chatbot` y entrada de menú para `Administrador`, `Tecnico` y `Consulta`. Proteger ruta y componente con autorización, además de las validaciones de Application. Construir con los componentes disponibles en Radzen.Blazor 6.0.0: lista de mensajes, entrada de texto y botón de envío; no asumir un componente de chat que requiera actualizar Radzen.

La vista solo captura la pregunta, invoca `IChatbotService` y presenta sus DTO. No calcula generación, determina fallas, decide permisos de negocio ni llama a SDK, repositorios o clientes de Infrastructure. La conexión MCP y las llamadas a la API del modelo se realizan en el backend; no instalar Antigravity ni un cliente MCP en el navegador. Mostrar mensajes del usuario y del asistente, espera, error y disponibilidad de datos. Indicar que el asistente únicamente consulta información.

Permitir un turno activo por conversación, deshabilitar envíos duplicados mientras se procesa y cancelar al destruir el componente o perder la sesión. El texto escrito por el usuario y devuelto por el modelo se muestra como texto escapado, sin ejecutar HTML, scripts u órdenes. No persistir historial ni credenciales en almacenamiento del navegador. No se requiere streaming, exportación, archivos adjuntos ni historial entre sesiones.

### P2. Comportamiento visible

La respuesta identifica parque, período/unidad y fecha de consulta cuando corresponda. La interfaz permite enviar otra pregunta tras un error, sin reemplazar un error con una respuesta inventada. La denegación de mantenimiento no bloquea otras consultas autorizadas. No mostrar mensajes técnicos del SDK, trazas ni credenciales al usuario.

## V. Verificación prevista

Las pruebas requeridas en `requirements.md` se cubren mediante escenarios integrados en `SolarUY.Integration.Tests`, sin introducir pruebas unitarias nuevas para Application, Infrastructure o Presentation. No hay comportamiento nuevo de Domain que requiera pruebas unitarias adicionales. Reutilizar xUnit y los mecanismos existentes; no añadir bibliotecas de pruebas sin autorización.

### V1. Funciones, datos y respuestas

Integrar los servicios reales de Application, adaptadores y repositorios con un proveedor de IA controlado en el límite externo, usando los puntos de extensión de los paquetes aprobados. Ejecutar un cliente y servidor MCP reales con el transporte aprobado; comprobar inicialización, descubrimiento y llamada de herramientas a través de Agent Framework, sin reemplazar MCP por funciones locales ni sustituir la autorización por dobles. Comprobar nombres/esquemas, rechazo de herramientas ajenas y tratamiento de un catálogo incompleto o incompatible. Los datos de prueba conocidos deben cubrir:

1. Estado actual y potencia del parque/equipos, con instante de consulta y sin usar histórico como telemetría actual.
2. Generación diaria y consultas diaria/mensual/anual idénticas a `IHistoricoGeneracionService` para el mismo filtro, incluyendo unidades, cobertura, huecos y procedencia.
3. Salud actual fallada y estado registrado `Falla`, distinguiendo su origen; cero potencia no implica falla.
4. Pendientes reales del parque: administrador, técnico con tareas propias y ajenas, y usuario `Consulta` sin acceso; excluir completadas y canceladas.
5. «¿Cómo está funcionando todo hoy?» con varios parques, fecha local de Uruguay, fuentes parciales y apartados no autorizados; cifras y estados coinciden con las consultas, sin completarlos artificialmente.
6. Parque inexistente, nombre ambiguo, ausencia de lecturas, cero válido y colecciones vacías diferenciados de una fuente fallida.

### V2. Seguridad y tolerancia a fallos

Probar acceso al chat, descubrimiento e invocación directa al servidor MCP con los tres roles, anonimato, identidad sin identificador y roles ajenos. Probar filtros manipulados, selección de otro parque/responsable, sesión de usuario ausente/no autenticada, identidad cambiada durante un turno y reutilización de sesión MCP entre usuarios; no transmitir ni mostrar información fuera del alcance. Un identificador de sesión o argumentos de usuario/rol no deben permitir suplantación. No corresponden pruebas de tokens HTTP porque el transporte confirmado no expone ese endpoint.

Verificar «Desactivá el panel 3», creación/edición/borrado, suplantación y solicitudes de ignorar restricciones, incluidas instrucciones maliciosas en descripciones de datos. Capturar llamadas externas y comparar datos antes/después para comprobar que no hay escrituras, órdenes, sincronización ni adquisición de lecturas causada por el chat. Probar además un modelo que intenta invocar una función de escritura desconocida.

Simular caída y tiempo máximo del modelo, desconexión/error de protocolo del servidor MCP, caída del inversor, fallo del histórico/mantenimiento y cancelación. Verificar mensajes seguros, resultados parciales explícitos y uso posterior de la aplicación, sin recurrir a consultas locales que omitan MCP ante una falla. Utilizar credenciales ficticias reconocibles de IA y MCP para comprobar que no aparecen en mensajes, resultados de herramientas, HTML, recursos del navegador ni registros, y que los tokens MCP no llegan a la API del modelo. Verificar ausencia de herramientas de escritura en los catálogos publicado y efectivo.

### V3. Panel, integración real y regresión

Verificar el recorrido del panel con servicios integrados y revisar en navegador autenticación, envío, respuesta, espera, rechazo de órdenes, manejo de error y aislamiento entre usuarios. Los dobles deterministas del proveedor verifican escenarios repetibles; registrar por separado una comprobación con **Ollama/qwen3:8b reales**, cliente/servidor MCP y MariaDB aislada, que demuestre invocación real por MCP y una respuesta fundamentada, sin exigir una redacción literal. La comprobación opcional de OpenAI requiere habilitación explícita y clave externa; no es un prerrequisito para usar SolarUY. No requerir Antigravity para estas verificaciones.

Utilizar `SolarUY.Integration.Tests/Verificar-Chatbot.ps1` para preparar en memoria la conexión a `solaruy_req007_test_req017` a partir de secretos locales, sin imprimirlos. El modo normal ejecuta escenarios deterministas; `-Ollama` ejecuta por defecto las comprobaciones del modelo local real, incluida la conversación de varios turnos, separadas de los hosts HTTP de pruebas para limitar memoria; `-OpenAI` selecciona las comprobaciones opcionales de ese proveedor; `-Navegador` habilita el servidor temporal de loopback. `-SinCompilar` reutiliza binarios y `-Salida` elige una carpeta propia bajo `bin/` si Windows bloquea una salida anterior. El valor inicial es `Req017Verified`. Registrar omisiones y limitaciones por modo. Respetar el aviso del README sobre no ejecutar cargas de volumen con el disco casi lleno; una exclusión por ese motivo debe quedar documentada. El verificador usa un tiempo de preparación SQL de 120 segundos, conservando los umbrales medidos existentes.

Usar exclusivamente la base aislada definida en `SolarUY.Integration.Tests/README.md` (`SOLARUY_HISTORICO_TEST_CONNECTION`, prefijo `solaruy_req007_test`) para escenarios con persistencia. Usar un simulador aislado o el límite de integración controlado para comprobar ausencia de órdenes; no modificar equipos o datos habituales. No habilitar rutas de autenticación de pruebas en producción.

Ejecutar y registrar:

- `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` tras cambios de código, corrigiendo el código sin alterar los guardarraíles.
- `dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter 'FullyQualifiedName~Chatbot'` para los escenarios nuevos, cuyos nombres incluyen `Chatbot`.
- `dotnet test SolarUY.sln` con los prerrequisitos documentados de las suites existentes.

Informar resultados reales, fallos, omisiones y recorridos manuales pendientes. No declarar verificados SQL, navegador o proveedor real por el solo uso de dobles o pruebas omitidas.

## C2. Cierre y documentación

Al implementar, crear o actualizar `implementations.md` en esta carpeta con cambios, archivos tocados, decisiones de C1 resueltas, configuración externa de IA/MCP, catálogo de herramientas y resultados de pruebas, incluidas omisiones y limitaciones. Crear o actualizar también `resumen.md` con una síntesis breve y fácil de leer de lo realizado. Marcar las tareas únicamente cuando sus resultados estén efectivamente comprobados.


## Conexión de Ollama desde Docker

El endpoint conserva loopback como valor predeterminado. La configuración externa `Chatbot:Ollama:PermitirHostDocker=true` admite únicamente el hostname exacto `host.docker.internal`; no habilita destinos arbitrarios. Compose fija `http://host.docker.internal:11434/`. Ollama debe aceptar la conexión de la red Docker. No se modifica el catálogo MCP ni la autorización de consultas.
