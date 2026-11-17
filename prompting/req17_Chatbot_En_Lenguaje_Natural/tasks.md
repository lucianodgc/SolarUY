# Tareas: REQ017 — Chatbot en lenguaje natural (solo consultas)

Fuente única de alcance técnico: [specs.md](specs.md). Las referencias entre paréntesis identifican las secciones que justifican cada tarea. El estado refleja únicamente lo verificado; la evidencia, resultados y limitaciones se detallan en [implementations.md](implementations.md). La inspección manual en navegador se conserva pendiente.

## Preparación y aclaraciones

- [x] 1. Resolver y documentar las decisiones necesarias para integrar el modelo existente por API y el cliente/servidor MCP. Los contratos de Application pueden avanzar antes de completar esta etapa; cada integración depende de sus aclaraciones. (C0, C1)
  - [x] 1.1. Registrar la decisión confirmada: Ollama local con `qwen3:8b` como predeterminado, OpenAI como opción futura y MCP interno. Documentar versiones compatibles con .NET 8 y ausencia de dependencia de créditos de OpenAI. (C1)
  - [x] 1.2. Confirmar los paquetes y versiones autorizados de Microsoft Agent Framework, el conector del modelo y el SDK MCP de C#; comprobar compatibilidad con `net8.0` y soporte de herramientas MCP. Documentar incompatibilidades para resolución del equipo sin actualizar la plataforma. (C0, C1)
  - [x] 1.3. Confirmar transporte, hospedaje y dirección del servidor MCP, documentando solo los ajustes necesarios sin asumir un nuevo contenedor, puerto público o despliegue para Antigravity. (C0, C1, I3)
  - [x] 1.4. Vincular cada servidor MCP interno al `IUsuarioActual` del circuito autenticado de Keycloak, capturar el propietario y revalidarlo en cada llamada; no abrir un endpoint HTTP ni transmitir credenciales. Registrar la decisión en `specs.md`. (C1, A3)

## Domain

- [x] 2. Identificar el modelo y las reglas existentes que consumirán las consultas, conservándolos sin cambios de esquema o comportamiento. (D1, D2)
  - [x] 2.1. Identificar las fuentes de `EstadoPanel.Falla`, `EstadoSaludPanel.Fallado` y `EstadoTareaMantenimiento.Pendiente` para que los resultados distingan estado registrado, salud actual y tareas pendientes. (D1, A2)
  - [x] 2.2. Identificar los contratos existentes de potencia W, energía kWh, períodos, cobertura y procedencia histórica que se reutilizarán sin crear cálculos nuevos ni migraciones. (D2, A2)

## Application

- [x] 3. Definir los contratos del módulo `SolarUY.Application.Chatbot`, sin dependencias de SDK de IA/MCP o de las capas externas. (A1)
  - [x] 3.1. Crear `PreguntaChatDto`, mensajes y `RespuestaChatDto`, con texto, estado y resultados que conserven objetivo, origen, instante/período, unidades y cobertura según corresponda. (A1)
  - [x] 3.2. Definir `IChatbotService.EnviarPreguntaAsync`, `IChatbotClient`, `IConsultaChatbotService` con las seis operaciones de A2 e `IEstadoEquiposConsultaClient` con operaciones exclusivamente de lectura. Incorporar `CancellationToken`. (A1, A2)
  - [x] 3.3. Definir resultados neutrales que distingan datos disponibles, vacío válido, entrada inválida, objetivo inexistente, acceso denegado y fuente no disponible. (A1, A5)
  - [x] 3.4. Mantener mensajes y resultados de `IChatbotClient` independientes de MCP; resolver la autenticación fuera de argumentos visibles para el modelo y evitar delegados de consulta que permitan omitir el protocolo. (A1, A3)

- [x] 4. Implementar `ConsultaChatbotService` y la autorización efectiva de cada función. (A2, A3)
  - [x] 4.1. Inyectar por constructor `IUsuarioActual` y las fuentes de consulta necesarias; exigir usuario autenticado, identificador estable y rol admitido en cada operación, también cuando la herramienta se invoque directamente en el servidor MCP. (A1, A3)
  - [x] 4.2. Implementar `ListarParquesAsync` reutilizando el descubrimiento autorizado; resolver identificadores y nombres sin inventarlos y devolver ambigüedad cuando corresponda. (A2)
  - [x] 4.3. Implementar `ConsultarEstadoActualAsync`: validar parque y pertenencia de nodos mediante el inventario de lectura, consultar `IEstadoEquiposConsultaClient` y devolver estado, potencia e instante sin sustituir telemetría por histórico. (A2, D1, D2)
  - [x] 4.4. Implementar `ConsultarPanelesConFallaAsync` con la salud actual y, cuando se incluyan estados registrados del inventario, distinguir su origen; no inferir fallas por cero potencia o ausencia de datos. (A2, D1)
  - [x] 4.5. Implementar `ConsultarGeneracionDiariaAsync` mediante `IHistoricoGeneracionService`, resolviendo «hoy» con `TimeProvider` en `America/Montevideo` y conservando cobertura y unidades. (A2, D2)
  - [x] 4.6. Implementar `ConsultarGeneracionHistoricaAsync` con objetivo validado y períodos diario/mensual/anual, resolviendo referencias mediante las opciones autorizadas del histórico y reutilizando `FiltroHistoricoDto`, fechas locales y límites existentes. (A2, D2)
  - [x] 4.7. Implementar `ConsultarMantenimientosPendientesAsync` mediante `ITareaMantenimientoService.ListarAsync`, filtrando pendientes y paneles del parque; conservar administrador con acceso global, técnico con tareas propias y denegación al rol `Consulta`. No devolver indicadores de acciones de gestión. (A2, A3)
  - [x] 4.8. Rechazar filtros que pretendan sustituir al actor o ampliar su alcance; no devolver al modelo datos ni metadatos restringidos cuando se deniega una consulta. (A3)

- [x] 5. Implementar `ChatbotService` y las reglas del turno. (A1, A3–A5)
  - [x] 5.1. Validar pregunta no vacía, autenticar y coordinar `IChatbotClient` mediante inyección por constructor; revalidar acceso antes de devolver el resultado y propagar cancelación. (A1, A3, A5)
  - [x] 5.2. Mantener contexto conversacional en memoria del circuito, aislado por usuario; aceptar solo entradas de usuario, sin roles de sistema ni resultados de herramientas aportados por el navegador. Descartar contexto y respuestas pendientes al perder o cambiar identidad. (A1, A5)
  - [x] 5.3. Conservar los resultados estructurados de las consultas para respaldar la respuesta y descartar afirmaciones operativas sin consultas exitosas del turno. No reutilizar el historial como evidencia actual. (A4)
  - [x] 5.4. Coordinar el resumen «¿Cómo está funcionando todo hoy?» mediante `PlanResumenChatbot`: consultar por MCP los cuatro apartados de cada parque autorizado antes de redactar, diferenciando potencia/energía y apartados parciales, sin datos o no autorizados. Conservar las consultas específicas del parque indicado. (A4)
  - [x] 5.5. Establecer el comportamiento de rechazo de órdenes y de instrucciones que intenten cambiar permisos, revelar secretos o ampliar capacidades, con explicación de solo consulta. (A4)
  - [x] 5.6. Traducir errores de IA, MCP y fuentes, datos insuficientes y cancelación a resultados seguros; distinguir una fuente fallida de una colección vacía o un cero, sin recurrir a funciones locales que omitan MCP ante una falla. (A5)
  - [x] 5.7. Atender saludos y cortesía con texto local definido en servidor, sin exigir datos ni invocar IA; reconocer solo mensajes completos y conservar autenticación, revalidación e historial. Distinguir «cómo estás» y admitir variantes compuestas como «hola, qué podemos hacer hoy» mediante patrones acotados de capacidades. Verificados consultas mixtas por MCP, tres roles, identidad inválida/cambiada, historial, rechazo de órdenes y descarte de respuestas sin evidencia. Último resultado: 40 casos de integración enfocados y 9 de arquitectura correctos, sin fallos ni omisiones, salida `Req017Cortesia`. La captura del usuario acredita los saludos iniciales; no se ejecutó la revisión visual posterior a la ampliación. (A1, A3, A4)

## Infrastructure

- [x] 6. Implementar `EstadoEquiposConsultaClient` sin efectos de escritura en equipos ni datos. (I2)
  - [x] 6.1. Adaptar exclusivamente `ObtenerEstadoPanelesAsync`, `ObtenerEstadoVolcadoRedAsync` y `ObtenerEstadoEncendidoAsync` del cliente existente para los nodos previamente autorizados. Conservar instante, unidades y alcance real de cada indicador. (I2, D2)
  - [x] 6.2. Devolver errores controlados ante indisponibilidad o inventario desconocido, sin sincronizar para corregirlo; evitar las rutas actuales de `InversorService` y `TelemetriaEnVivoService` que sincronizan inventario. (C0, I2)
  - [x] 6.3. Comprobar que el recorrido completo del chat no llama a sincronización, adquisición de lecturas, órdenes, guardado EF ni repositorios de escritura, y que no comparte concurrentemente un contexto EF o servicio incompatible. (I2)

- [x] 7. Implementar el servidor MCP de SolarUY y `ChatbotClient` con las decisiones confirmadas en la etapa 1. (C1, I1, I4)
  - [x] 7.1. Incorporar únicamente las dependencias aprobadas de Agent Framework, el conector del modelo y el SDK MCP, manteniendo sus tipos fuera de Domain y Application. (C0, C1, I1, I4)
  - [x] 7.2. Crear los adaptadores del servidor MCP en Infrastructure para las seis herramientas de A2, con nombres, descripciones, esquemas y resultados estructurados; delegar en `IConsultaChatbotService` sin duplicar cálculos o permisos. (A2, I4)
  - [x] 7.3. Implementar descubrimiento e invocación por MCP del catálogo cerrado; validar argumentos y traducir resultados/errores de Application, sin publicar servicios completos por reflexión ni herramientas administrativas. (I4)
  - [x] 7.4. Implementar el cliente MCP dentro de `ChatbotClient`, conectar al servidor configurado y validar nombres/esquemas contra la lista permitida antes de ofrecer herramientas a Agent Framework. Rechazar herramientas ajenas o incompatibles y tratar las ausentes como no disponibles. (A2, I1)
  - [x] 7.5. Integrar la API del modelo existente y las herramientas obtenidas por MCP; ejecutar las invocaciones desde el backend, sin delegados locales de Application ni credenciales MCP enviadas al proveedor. (A1, I1)
  - [x] 7.6. Configurar las instrucciones de fundamentación en datos reales, resumen diario y rechazo de órdenes; tratar preguntas y textos de datos como contenido que no puede cambiar permisos, dirección MCP ni catálogo. (A4, I1)
  - [x] 7.7. Traducir fallas del proveedor y del protocolo MCP, propagar cancelación/tiempo máximo y liberar recursos del cliente y sesiones al terminar su ámbito o perder identidad. (A5, I1, I3)

  - [x] 7.8. Implementar `OllamaChatClient` mediante la API nativa local y `ProveedorModeloChatbotFactory`, con `qwen3:8b`, endpoint loopback, `think=false`, contexto configurado y conversión de llamadas/resultados de herramientas a `IChatClient`. Mantener OpenAI opcional sin seleccionarlo automáticamente ante errores de Ollama. (C1, I1, I3)
  - [x] 7.9. Mejorar continuidad y peticiones compuestas con planificación semántica estructurada en Agent Framework: validar planes cerrados, renovar datos por MCP, resolver referencias autorizadas, completar apartados y pedir aclaración sin inventar selecciones. Admitir salida JSON en Ollama y un solo intento correctivo sin consultas, conservando seis herramientas, permisos y evidencia. Reproducida la conversación real de tres turnos: listado, ampliación y cantidad/detalle, con cuatro apartados de ambos parques y conteo exacto. Resultado final: 63 casos controlados, 1 caso real de conversación y 9 de arquitectura correctos, sin fallos ni omisiones, salida `Req017Dialogo`. Comprobadas ambigüedad, alcance, planes inválidos, permisos y contexto; recorrido visual posterior pendiente. (A1, A3, A4, I1, V1, V2, V3)

- [x] 8. Configurar IA/MCP y registrar dependencias con los ámbitos autenticados correspondientes. (A3, I3, I4)
  - [x] 8.1. Definir opciones `Chatbot` para el proveedor y `Chatbot:Mcp` para la conexión aprobada; validar ambas y dejar el chat indisponible de forma controlada cuando falten sin bloquear otras pantallas ni habilitar MCP anónimo. (C1, I3)
  - [x] 8.2. Configurar Ollama sin clave de OpenAI y mantener la clave opcional de OpenAI en `user-secrets`/variables de entorno; conservar la identidad en memoria del backend, sin credenciales MCP estáticas ni claves/tokens en mensajes, herramientas, DTO o registros. (I3)
  - [x] 8.3. Registrar servicios/clientes en los puntos de composición y alojar/mapear el transporte en el host aprobado, documentando solo ajustes necesarios de hospedaje/autenticación sin modificar workers. (C1, I3, I4)
  - [x] 8.4. Componer el servidor MCP en el ámbito autenticado del circuito, capturar y comprobar su propietario mediante `IUsuarioActual` antes/después de cada llamada y antes del catálogo. Autorizar cada operación en Application sin otro `AuthenticationStateProvider`. (A3, I3, I4)
  - [x] 8.5. Aislar credenciales y sesiones MCP por usuario, impedir suplantación mediante argumentos/encabezados no validados y evitar cuentas compartidas o identificadores de sesión usados como prueba de identidad. (A3, I1)

## Presentation

- [x] 9. Crear el panel de conversación y su acceso autorizado. (P1, P2)
  - [x] 9.1. Crear `Chatbot.razor` en `/chatbot` y agregar la entrada de menú para `Administrador`, `Tecnico` y `Consulta`, con protección de ruta/componente. (P1)
  - [x] 9.2. Construir lista de mensajes, entrada y envío con componentes Radzen instalados e invocar únicamente `IChatbotService`, manteniendo IA/MCP en el backend y sin instalar Antigravity ni otro cliente en el navegador. (P1)
  - [x] 9.3. Mostrar espera, errores seguros, rechazo de acciones y respuestas con parque, período/unidad e instante; informar que el asistente es de solo consulta. (P1, P2)
  - [x] 9.4. Impedir envíos duplicados durante un turno, cancelar al cerrar/perder sesión y permitir nuevos envíos tras errores. Renderizar texto escapado sin ejecutar contenido ni persistir el historial en el navegador. (A1, A5, P1, P2)
  - [x] 9.5. Conservar y restaurar la conversación visible en el mismo circuito al salir y volver al panel; cancelar el turno en curso sin borrar intercambios anteriores. Verificadas reapertura tras visitar otro componente, nueva conversación, mensajes de error/rechazo, copia independiente del historial, cambio/pérdida de identidad, cambio de rol, aislamiento entre circuitos y cancelación al salir. Resultado: 12 casos de integración enfocados y 9 de arquitectura correctos, sin fallos ni omisiones, salida `Req017Conversacion`. No se declara ejecutado el recorrido manual posterior en navegador. (A1, A5, P1)

## Verificación

- [x] 10. Preparar escenarios integrados `Chatbot` con datos conocidos, proveedor controlado, cliente/servidor MCP reales y servicios reales de Application, sin pruebas unitarias nuevas para las capas externas. (V1–V3)
  - [x] 10.1. Reutilizar la base aislada de `SolarUY.Integration.Tests/README.md` y un simulador aislado o límite de integración controlado; preparar parques, nodos, paneles, lecturas y tareas que permitan comprobar también accesos ajenos y fuentes sin datos. (V1, V3)
  - [x] 10.2. Verificar inicialización, descubrimiento, nombres/esquemas e invocación a través del protocolo MCP y Agent Framework; cubrir catálogos incompletos/incompatibles y herramientas ajenas, sin reemplazar MCP por funciones locales ni la autorización por dobles. (V1, V2)
  - [x] 10.3. Verificar estado actual, potencia e instante, fallas actuales frente a registradas y cero sin falla inferida; comprobar que el estado actual no procede de consultas históricas. (V1)
  - [x] 10.4. Comparar generación diaria e histórico diario/mensual/anual con el servicio existente para el mismo filtro, incluyendo kWh, cobertura, huecos y procedencia desconocida. (V1)
  - [x] 10.5. Verificar pendientes por parque y rol: administrador, técnico con tareas propias/ajenas y `Consulta` denegado, excluyendo tareas completadas y canceladas. (V1, V2)
  - [x] 10.6. Verificar el resumen «¿Cómo está funcionando todo hoy?» con múltiples parques y día de Uruguay, datos parciales y apartados no autorizados; contrastar cifras/estados con los resultados reales de prueba. (V1)
  - [x] 10.7. Verificar parque inexistente, nombre ambiguo, ausencia de lecturas, cero válido y colección vacía sin completar datos artificialmente. (V1)

- [x] 11. Verificar permisos, ausencia de efectos de escritura y recuperación de fallos. (V2)
  - [x] 11.1. Probar descubrimiento e invocación directa de herramientas MCP con los tres roles, anonimato, identificador ausente, roles ajenos y filtros manipulados; verificar que no se filtran datos de otros responsables o alcances. (V2)
  - [x] 11.2. Verificar rechazo de «Desactivá el panel 3», altas, cambios, borrados, suplantación e instrucciones maliciosas en preguntas y descripciones; incluir un intento del modelo de llamar a una herramienta de escritura desconocida. (V2)
  - [x] 11.3. Capturar llamadas externas y comparar datos antes/después para demostrar que no hay escrituras, órdenes, sincronización ni adquisición de lecturas originadas por el chat. (V2)
  - [x] 11.4. Simular caída/tiempo máximo de IA, desconexión/error de protocolo MCP, caída del inversor, errores de histórico/mantenimiento y cancelación; comprobar errores seguros, respuestas parciales explícitas y uso posterior sin consultas locales que omitan MCP. (V2)
  - [x] 11.5. Verificar aislamiento entre usuarios, intento de reutilizar sesión MCP ajena y cambio/pérdida de identidad durante el turno, descartando respuestas y contexto anteriores. (V2, V3)
  - [x] 11.6. Comprobar con credenciales ficticias de IA/MCP que no hay secretos en mensajes, herramientas, HTML, recursos ni registros, que los tokens MCP no llegan al proveedor y que los catálogos publicado/efectivo carecen de herramientas de escritura. (V2)
  - [x] 11.7. Rechazar sesiones de usuario no autenticadas, sin identificador, con rol ajeno o con propietario cambiado, y suplantación mediante argumentos o identificadores de sesión. No corresponde autenticación HTTP en el transporte interno confirmado. (V2)
  - [x] 11.8. Corregir y verificar el aviso final de consulta vacía: ausencia válida de fallas/pendientes como resultado correcto; ausencia real de equipos/lecturas con aviso específico; errores y permisos conservados mediante escenarios integrados con Agent Framework y MCP. Resultado: 9 casos de integración enfocados y 9 de arquitectura correctos, sin omisiones ni fallos, salida `Req017Avisos`. La comprobación visual posterior a este ajuste no se ejecutó. (A1, A5, V1, V2)

- [x] 12. Validar el panel, la integración real y la regresión del proyecto, registrando evidencia y omisiones reales. (V3)
  - [x] 12.1. Completar la inspección manual en navegador de acceso autenticado, envío/respuesta, espera, rechazo, errores y aislamiento. Las verificaciones HTTP y de render integrado pasaron. Computer Use fue detenido por no poder validar la URL de Chrome. Las capturas del usuario acreditan acceso como administrador y respuesta al resumen: primero con fuentes no disponibles, cuando el simulador no era accesible, y después con potencia y generación parcial de ambos parques. La última captura muestra Markdown literal y un aviso genérico final sin identificar la consulta vacía; se registran como observaciones, sin cambiar el alcance. Siguen pendientes los recorridos visuales de espera, rechazo, recuperación de error y aislamiento; las capturas no acreditan la revisión completa ni un contraste independiente de las cifras. (V3)
  - [x] 12.2. Completar una repetición final con Ollama/`qwen3:8b`, MCP, MariaDB y simulador aislados. Histórico y resumen de dos parques pasaron secuencialmente con contexto 16384, `num_batch=128`, `use_mmap=true` y `keep_alive="60s"`. Se detuvieron temporalmente Jenkins/SonarQube con autorización y se restauraron: Jenkins HTTP 200, SonarQube `UP`. Modelo descargado y sin host visual escuchando al finalizar. OpenAI sigue opcional. (V3)
  - [x] 12.3. Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` tras cambios de código y corregir incumplimientos en el código sin modificar guardarraíles. Último código, incluida planificación de consultas: 9 correctas, 0 fallos, salida `Req017Dialogo`. (V3)
  - [x] 12.4. Repetir únicamente las verificaciones pendientes, según la indicación posterior del usuario: las dos pruebas reales pasaron separadas y la prueba integrada del adaptador pasó con los parámetros finales. Se conservan como antecedentes los fallos anteriores, sin repetir los 48 escenarios ya aprobados ni toda la solución. La comparación de nombres en el resumen ignora mayúsculas según V3; no se relajaron permisos, MCP ni comprobaciones de datos. (V3)
  - [x] 12.5. Ejecutar la solución con los prerrequisitos existentes mediante `Verificar-Chatbot.ps1 -Completa`, que usa `bin/Req017Verified/`; registrar fallos, omisiones y verificaciones no ejecutadas sin declararlas aprobadas. Resultado anterior: 289 correctas, 9 omitidas, 0 fallos, incluida la carga de volumen. No se repitió la solución completa tras los ajustes locales de memoria, conforme a la nueva indicación del usuario; se verificaron arquitectura, adaptador y las dos consultas reales afectadas. (V3)
  - [x] 12.6. Comprobar una consulta real después de liberar recursos ante la nueva indisponibilidad reportada por el usuario. C: pasó de unos 250 MiB a 14,9 GiB libres. Se ejecutó únicamente `Chatbot_OllamaRealResumenDeHoyConMariaDbYSimuladorTcpAislado`: 1 correcta, 0 fallos, 0 omisiones, con binarios `Req017Avisos`, base aislada y Jenkins/SonarQube/Keycloak activos. Contexto 16384 y lote 128 conservados. El simulador terminó con la prueba y el modelo se descargó explícitamente; no se declara una prueba de carga ni la revisión visual completa. (C1, V3)

## Cierre / Documentación

- [x] Permitir Ollama remoto con habilitación explícita, URL base HTTP/HTTPS validada, parámetro OllamaRemoto para ambos modos y opciones persistentes de Compose.
- [x] Verificar con dos PCs reales y, si corresponde, con VPN; comprobar conectividad desde los contenedores.

- [x] Actualizar la pregunta con el evento de entrada y el estado del botón mientras se escribe, conservando el bloqueo durante la consulta.
- [x] Comprobar visualmente escritura, borrado y pegado sin perder el foco en el chat.

- [x] 13. Crear o actualizar implementations.md en la misma carpeta con el detalle de los cambios realizados, los archivos tocados y el resultado de las pruebas. Crear o actualizar tambien resumen.md en esa carpeta con una sintesis breve y facil de leer de lo realizado para el requerimiento. Incluir las decisiones resueltas, configuración externa de IA/MCP, catálogo de herramientas, limitaciones y verificaciones pendientes, y marcar solo tareas efectivamente completadas. (C2)
