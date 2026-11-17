# Implementación de REQ017 — Chatbot de consultas


Ollama remoto: se incorpora `PermitirRemoto` (false por defecto), validación de URL base HTTP/HTTPS y `Iniciar-Demo.ps1 -OllamaRemoto <URL>` para web local y ambas webs Docker. Compose admite variables externas persistentes documentadas en `.env.example` y `docs/Configuracion.md`. Se conservan el modelo, herramientas MCP, permisos, bloqueo de redirecciones y validación de certificados. Verificación: 17 pruebas de integración del proveedor y 9 de arquitectura aprobadas; parámetros locales/Docker y restauración del entorno comprobados con comandos sustituidos, sin iniciar servicios. Conexión entre dos PCs reales o mediante VPN pendiente.

Actualización: `Chatbot.razor` actualiza el texto en `oninput` y habilita Enviar pregunta mientras se escribe; se conserva el bloqueo para espacios y consultas en curso y se explicita el estilo primario. Compilación y 9 pruebas de arquitectura aprobadas. Pendiente comprobar escritura/borrado/pegado en navegador. No se cambiaron el modelo ni los parámetros de rendimiento ni se midió la latencia en el equipo.

## Resultado y decisiones

Se implementó el panel `/chatbot` en Blazor/Radzen, los servicios de Application, las seis herramientas MCP de lectura y la integración con Microsoft Agent Framework. El proveedor predeterminado es **Ollama local con `qwen3:8b`**, por decisión explícita del usuario. OpenAI permanece opcional y no se utiliza automáticamente cuando falla Ollama.

Cliente y servidor MCP intercambian mensajes JSON-RPC reales por streams/pipes internos, con un par por turno y propietario autenticado. No se abrió un endpoint MCP público ni se integró Antigravity. Application conserva la autorización mediante `IUsuarioActual`; el servidor comprueba también al propietario antes/después de cada invocación y antes del catálogo.

Se conservaron .NET 8, EF Core 8, Pomelo 8.0.3, MariaDB 11.8 y Radzen 6.0.0. Se fijaron `Microsoft.Agents.AI` y `Microsoft.Agents.AI.OpenAI` 1.28.0 y `ModelContextProtocol.Core` 2.1.0, compilados para `net8.0`. El adaptador de Ollama usa `IChatClient`, `HttpClient` y JSON de .NET, sin otro paquete. En el equipo se verificaron Ollama 0.40.16 y `qwen3:8b`, digest `500a1f067a9f`, Q4_K_M.

## Comportamiento implementado

- Descubrimiento autorizado de parques, estado actual, generación diaria, fallas, pendientes e histórico diario/mensual/anual.
- Reutilización del histórico, monitoreo para listar parques, inventario y mantenimiento existentes. No hay tablas, migraciones ni cálculos de energía nuevos.
- Estado actual obtenido por las tres operaciones de lectura del inversor, sin sincronizar inventario ni adquirir/guardar lecturas. Se comprueba pertenencia y coherencia de los paneles recibidos.
- Diferenciación de potencia W y energía kWh, fecha de Uruguay, cobertura y procedencia histórica; las fallas registradas se distinguen de la salud actual.
- Mantenimiento: administrador consulta las tareas autorizadas, técnico solo las propias y rol `Consulta` recibe denegación. No se devuelven acciones de gestión.
- Catálogo cerrado y validación de nombres, esquemas y argumentos. No se ofrecen SQL, shell, administración, escritura ni sustitución del actor.
- Historial en memoria del circuito, aislamiento de identidad, cancelación y rechazo de envíos simultáneos. El panel escapa el texto, incluidos fragmentos HTML.
- Para «¿Cómo está funcionando todo hoy?», Application marca el plan general y el cliente ejecuta por MCP los cuatro apartados de cada parque autorizado antes de pedir la redacción. Esto corrige la omisión de apartados observada cuando se dejaba esa selección exclusivamente al modelo.
- Indisponibilidad controlada ante fallas de proveedor, MCP o fuentes. Se descartan respuestas operativas sin consultas de respaldo y se conservan avisos explícitos sobre consultas incompletas.
- Registro explícito `AddChatbotInfrastructure` en el arranque. El módulo básico de Infrastructure no exige servicios del chat a los hosts existentes de reportes o monitoreo.

El catálogo MCP publicado contiene únicamente `ListarParquesAsync`, `ConsultarEstadoActualAsync`, `ConsultarGeneracionDiariaAsync`, `ConsultarPanelesConFallaAsync`, `ConsultarMantenimientosPendientesAsync` y `ConsultarGeneracionHistoricaAsync`. Los argumentos funcionales y permisos de cada operación están en A2/A3 de [specs.md](specs.md).

## Configuración

Los valores no sensibles están en `SolarUY.Presentation/appsettings.json`:

| Opción | Valor predeterminado |
| --- | --- |
| `Chatbot:Proveedor` | `Ollama` |
| `Chatbot:Modelo` | `qwen3:8b` |
| `Chatbot:Ollama:Endpoint` | `http://127.0.0.1:11434/` |
| `Chatbot:Ollama:Contexto` | `16384` |
| `Chatbot:TiempoMaximoSegundos` | `300` |
| `Chatbot:Mcp:Transporte` | `Interno` |

Ollama debe estar ejecutándose con el modelo disponible. Solo se admiten endpoints loopback sin credenciales, consulta ni redirecciones. Se usa `/api/chat`, `stream=false` y `think=false`; no se muestra razonamiento interno. El adaptador fija `num_batch=128`, `use_mmap=true` y `keep_alive="60s"` para reducir búferes y memoria anónima y liberar el modelo tras inactividad. No se requiere clave ni saldo de OpenAI.

Para seleccionar OpenAI en el futuro, configurar explícitamente `Chatbot:Proveedor=OpenAI`, `Chatbot:Modelo=gpt-6-luna` y `Chatbot:OpenAI:ApiKey` mediante `user-secrets` o `Chatbot__OpenAI__ApiKey`. No guardar el valor en archivos versionados. Responses usa `store=false`. Sus diagnósticos experimentales se aceptan únicamente dentro de la fábrica opcional de OpenAI.

## Archivos del cambio

### Application

- `SolarUY.Application/Chatbot/ContratosChatbot.cs`
- `SolarUY.Application/Chatbot/ChatbotService.cs`
- `SolarUY.Application/Chatbot/ConsultaChatbotService.cs`
- `SolarUY.Application/Chatbot/PlanResumenChatbot.cs`

### Infrastructure

- `SolarUY.Infrastructure/SolarUY.Infrastructure.csproj`
- `SolarUY.Infrastructure/ChatbotDependencyInjection.cs`
- `SolarUY.Infrastructure/Chatbot/CatalogoMcpChatbot.cs`
- `SolarUY.Infrastructure/Chatbot/ChatbotClient.cs`
- `SolarUY.Infrastructure/Chatbot/ChatbotOptions.cs`
- `SolarUY.Infrastructure/Chatbot/ConexionMcpChatbot.cs`
- `SolarUY.Infrastructure/Chatbot/ConsultaMcpFunction.cs`
- `SolarUY.Infrastructure/Chatbot/EstadoEquiposConsultaClient.cs`
- `SolarUY.Infrastructure/Chatbot/OllamaChatClient.cs`
- `SolarUY.Infrastructure/Chatbot/OpenAiModeloChatbotFactory.cs`
- `SolarUY.Infrastructure/Chatbot/ProveedorModeloChatbotFactory.cs`
- `SolarUY.Infrastructure/Chatbot/ServidorMcpChatbot.cs`

### Presentation

- `SolarUY.Presentation/Program.cs`
- `SolarUY.Presentation/appsettings.json`
- `SolarUY.Presentation/Components/Layout/NavMenu.razor`
- `SolarUY.Presentation/Components/Pages/Chatbot.razor`
- `SolarUY.Presentation/Components/Pages/Chatbot.razor.css`

### Pruebas y documentación

- `SolarUY.Integration.Tests/ChatbotCatalogoIntegrationTests.cs`
- `SolarUY.Integration.Tests/ChatbotEscenario.cs`
- `SolarUY.Integration.Tests/ChatbotIntegrationTests.cs`
- `SolarUY.Integration.Tests/ChatbotInversorControlado.cs`
- `SolarUY.Integration.Tests/ChatbotMariaDbIntegrationTests.cs`
- `SolarUY.Integration.Tests/ChatbotPanelIntegrationTests.cs`
- `SolarUY.Integration.Tests/ChatbotProveedorIntegrationTests.cs`
- `SolarUY.Integration.Tests/ChatbotPruebasHost.cs`
- `SolarUY.Integration.Tests/ChatbotSeguridadIntegrationTests.cs`
- `SolarUY.Integration.Tests/Verificar-Chatbot.ps1`
- `requirements.md`, `specs.md`, `tasks.md`, `implementations.md` y `resumen.md` de esta carpeta.

No se cambiaron Domain, los servicios existentes ni los guardarraíles de `CodeQuality.Tests`. Las carpetas protegidas `.agents/rules/` y `.agents/skills/` no se modificaron.

## Verificaciones y resultados

| Verificación | Resultado observado |
| --- | --- |
| Solución completa, `Verificar-Chatbot.ps1 -Completa` | **289 correctas, 9 omitidas, 0 fallos**, incluida la prueba de un millón de lecturas. Snapshot anterior al ajuste final de contexto 16384 → 8192 y al diagnóstico HTTP local. |
| Arquitectura sobre el código con contexto 8192 | **9 correctas, 0 fallos**, usando `BaseOutputPath=bin/Req017LocalFinal/`. |
| Consultas reales con Ollama, Agent Framework, MCP y MariaDB | Histórico y resumen general pasaron individualmente con el modelo cargado. El histórico devolvió los 0,78 kWh conocidos; el resumen consultó los cuatro apartados de ambos parques y conservó los datos. |
| Suite de REQ017 con contexto 8192, antes de precargar el modelo | **48 correctas, 2 fallos de disponibilidad de Ollama HTTP 500, 3 omitidas**. Los casos fallidos fueron las dos pruebas reales; no se declaran verificados en esa repetición. |
| Repetición con precarga y suite mixta | Se observó también `OutOfMemoryException` al iniciar Kestrel; la ejecución quedó afectada por falta de recursos. Se separaron los modos de verificación para evitar ese pico. |
| Dos pruebas reales separadas anteriores, contexto 8192, base `solaruy_req007_test_req017_local` | **0 correctas, 2 fallos por HTTP 500 de Ollama**. La precarga local también devolvió HTTP 500 en ese momento. Este antecedente fue seguido por la verificación corregida descrita más abajo. |
| Panel automatizado | Acceso HTTP de los tres roles, denegación anónima/rol ajeno, envío con servicios y MCP reales, HTML escapado y error seguro verificados. |
| Inspección visual | **Pendiente**, incluido el nuevo intento. Computer Use volvió a detenerse porque no pudo validar la URL de Chrome. El host temporal se cerró correctamente; no se declara inspeccionado el panel. |
| Credenciales | Cero coincidencias de la clave real de OpenAI en los archivos modificados/nuevos y en los archivos versionados. Las pruebas comprueban ausencia de clave en respuestas/registros y ausencia de credenciales en el payload del adaptador local. No se imprimieron claves ni conexiones. |

La solución completa sumó 9 pruebas de arquitectura, 57 de Domain, 16 existentes de Application y 207 de integración correctas. Las 9 omisiones fueron dos pruebas locales habilitables por separado, dos opcionales de OpenAI, el servidor visual de REQ017 y cuatro omisiones existentes de monitoreo/Nginx/reportes. Las pruebas opcionales de OpenAI se detuvieron tras observar `429 credit_balance_exhausted`; el usuario autorizó Ollama para evitar ese costo.

Los primeros fallos de composición de servicios se corrigieron separando el registro del módulo. Los tiempos de espera al preparar/limpiar el millón de filas se resolvieron con `DefaultCommandTimeout=120` exclusivamente en la conexión de pruebas, según el README existente. Se conservaron las exigencias de menos de cinco segundos por consulta y menos de 256 MiB adicionales. La ejecución completa posterior pasó sin fallos.

## Reproducción y limitaciones del entorno

El verificador lee los secretos locales sin mostrarlos, permite únicamente MariaDB local y una base con prefijo `solaruy_req007_test_`. El destino predeterminado es `solaruy_req007_test_req017`; `-BaseDePruebas` permite separar ejecuciones. Usa salida `bin/Req017Verified/`, configurable con `-Salida`, y admite `-SinCompilar`.

```powershell
# Escenarios deterministas; proveedores reales y servidor visual omitidos.
powershell -NoProfile -File SolarUY.Integration.Tests/Verificar-Chatbot.ps1

# Solo las comprobaciones del modelo local, sin recompilar.
powershell -NoProfile -File SolarUY.Integration.Tests/Verificar-Chatbot.ps1 -Ollama -SinCompilar -Salida Req017Pending

# Regresión completa: requiere MariaDB y simulador TCP según el README.
powershell -NoProfile -File SolarUY.Integration.Tests/Verificar-Chatbot.ps1 -Completa
```

Se encontraron presiones reales de memoria y espacio. Inicialmente Ollama respondió con HTTP 500 al cargar el modelo; una inferencia básica con 8192 funcionó, pero no acreditó el resumen completo. La continuación siguiente reemplaza esa configuración provisional por 16384 con ajustes de memoria verificados. No se cambió el modelo ni se sustituyó por un proveedor pago.

Algunos procesos de pruebas anteriores mantuvieron DLLs bloqueadas. Windows negó consultar su línea de comando y detenerlos, incluso fuera del sandbox. Se utilizaron salidas propias para compilar sin modificar procesos ajenos. El host temporal del panel se inició y cerró correctamente; eso no acredita la inspección visual. El simulador iniciado para la regresión también fue detenido.

La compilación de la solución mostró `NU1900` en el proyecto del simulador por indisponibilidad de la consulta de vulnerabilidades NuGet. No se suprimió esa advertencia ni se modificaron guardarraíles para obtener un resultado favorable.

## Continuación: diagnóstico de recursos y verificaciones pendientes

El usuario pidió continuar únicamente las verificaciones pendientes. Se mantuvieron Application, las seis herramientas MCP, los permisos, la interfaz y los servicios existentes. Los cambios de esta continuación se limitaron a `OllamaChatClient.cs`, el valor de contexto en `ChatbotOptions.cs`/`appsettings.json`, dos pruebas integradas relacionadas y la documentación.

### Causa observada y ajuste

Windows tenía 15,72 GiB de RAM, 0,86 GiB disponibles y 3,78 GiB de memoria virtual disponible; quedaban 7,77 GiB en C:. La RTX 5070 Ti tenía unos 14,3 GiB de VRAM libres. Ollama registró `cudaMalloc failed` tanto al reservar pesos como al reservar búferes de cálculo: la VRAM libre por sí sola no acreditaba capacidad de carga. WSL consumía unos 5,6 GiB privados y los contenedores de Jenkins/SonarQube sumaban unos 2,7 GiB.

Ollama 0.40.16 deshabilitaba automáticamente el mapeo de archivos en Windows/CUDA y elegía un lote de 1024. Reducir solo el lote no solucionó la carga. Con CI temporalmente detenido y `use_mmap=true`, la inferencia respondió. El lote 128 dejó un búfer CUDA de cálculo de 28 MiB con contexto 8192 y 30 MiB con contexto 16384, frente a la reserva fallida de unos 224 MiB. Se conservaron los pesos Q4_K_M, el modelo, el muestreo y todas las herramientas. La [implementación oficial del planificador](https://github.com/ollama/ollama/blob/v0.40.16/server/sched.go) y los [tipos de la API](https://github.com/ollama/ollama/blob/v0.40.16/api/types.go) respaldan esos parámetros.

El contexto 8192 mostró además una limitación funcional: el modelo omitía un parque en el resumen. Con contexto 16384, Ollama recibió un prompt completo de 12655 tokens y la comprobación de ambos parques pasó. Por eso se restituyó 16384 en lugar de reducir datos o quitar herramientas para ahorrar memoria. La caché KV correspondiente ocupa 2304 MiB en GPU; no se promete funcionamiento si otras aplicaciones agotan la memoria disponible. Preguntas con más datos o conversaciones extensas siguen sujetas al límite de contexto configurado.

La primera comprobación del resumen también detectó una comparación literal de mayúsculas (`Parque Histórico` frente a `Parque histórico`). Se corrigió únicamente esa comparación integrada para usar `OrdinalIgnoreCase`, conforme a V3; la verificación de ambos parques, invocaciones, permisos, secretos y datos sin cambios se conservó. El fallo posterior por omisión del segundo parque no se ocultó y motivó verificar el contexto completo.

### Resultado final de esta continuación

Todos los comandos se ejecutaron secuencialmente, sin solapar pruebas ni hospedar Kestrel durante las consultas del modelo real. Se usó `bin/Req017Pending/` y la base aislada `solaruy_req007_test_req017_pending`.

| Comprobación sobre la configuración final | Resultado |
| --- | --- |
| `CodeQuality.Tests`, contexto 16384 y adaptador final | **9 correctas, 0 fallos**. Guardarraíles sin modificaciones. |
| `Ollama_AdaptadorNativoInvocaMcpSinClaveDeOpenAiNiCredencialesEnElPayload` | **1 correcta**. Verifica contexto, lote, mapeo, permanencia, seis herramientas, llamada MCP y ausencia de credenciales en el payload. |
| `Chatbot_OllamaRealConsultaHistoricoPorAgentFrameworkYMcp` | **1 correcta**, sin recompilar y sin sobrescribir el contexto por entorno. Consulta real, 0,78 kWh esperados, evidencia MCP, comprobaciones de secretos y datos conservados. |
| `Chatbot_OllamaRealResumenDeHoyConMariaDbYSimuladorTcpAislado` | **1 correcta**, después del histórico, con valores finales predeterminados. Ambos parques, cuatro apartados por parque, rechazo de orden, simulador activo y datos sin cambios. |
| Host temporal de inspección visual | **1 correcta en su ciclo de vida**, iniciado y cerrado mediante `/pruebas/finalizar`. Esto no acredita la inspección visual. |
| Suite completa / 48 escenarios previamente aprobados | **No repetidos**, por indicación del usuario y porque no se cambiaron sus funcionalidades. Los resultados anteriores permanecen como antecedentes. |
| OpenAI | **No invocado** en esta continuación; no se consumieron créditos. |

Para reproducir únicamente los casos reales uno por vez, usar el comando anterior con `-Filtro FullyQualifiedName~Chatbot_OllamaRealConsultaHistoricoPorAgentFrameworkYMcp` y luego con `-Filtro FullyQualifiedName~Chatbot_OllamaRealResumenDeHoyConMariaDbYSimuladorTcpAislado`. `-SinCompilar` requiere haber compilado esta versión; las carpetas anteriores contienen binarios de configuraciones previas.

Jenkins y SonarQube se detuvieron temporalmente con autorización expresa y se volvieron a iniciar al terminar. Sus APIs de actividad rechazaron consultas anónimas; no se afirma haber verificado la ausencia de trabajos mediante ellas. No se eliminaron contenedores, volúmenes ni configuraciones. Restauración comprobada: **Jenkins `/login` HTTP 200, SonarQube `/api/system/status` `UP`**, y MariaDB siguió ejecutándose. El modelo se descargó explícitamente y `/api/ps` informó cero modelos cargados. El host visual dejó de escuchar en 5217; los simuladores de los casos reales se cerraron al finalizar cada prueba. No quedaron sesiones de prueba iniciadas en esta continuación.

La inspección visual permanece abierta: el navegador pudo observarse, pero al intentar abrir una pestaña Computer Use detuvo el control por no poder validar con seguridad la URL. Se obedeció la detención y se cerró el host desde su endpoint de pruebas. No se automatizó el login de Keycloak ni se completó un recorrido visual con proveedor real.

Queda pendiente únicamente la inspección visual de 12.1; la etapa 12 conserva su casilla abierta en `tasks.md`.

### Evidencia visual aportada por el usuario

Después de sincronizar externamente el secreto de login con el cliente actual de Keycloak, el usuario aportó una captura de `/chatbot` con sesión de administrador, la pregunta «¿Cómo está funcionando todo hoy?» y una respuesta visible para sus dos parques. Esto acredita visualmente acceso, envío y presentación de una respuesta; no acredita espera, rechazo, recuperación ni aislamiento entre usuarios.

La respuesta diferencia estado/fallas no disponibles, generación diaria sin datos y ausencia de tareas pendientes. La comprobación TCP posterior no pudo conectar con el simulador configurado en `127.0.0.1:5055`, y no se encontró un proceso `SolarUY.InversorSimulador` con ese nombre. Esta evidencia es consistente con la indisponibilidad de la telemetría; no demuestra que toda ausencia de datos históricos tenga la misma causa. No se inició el simulador, no se generaron lecturas ni se alteraron datos para sustituir esa evidencia. La tarea 12.1 permanece parcialmente verificada y abierta.

Una segunda captura del usuario muestra potencia en W y energía en kWh para Maldonado y Rocha, generación diaria explícitamente parcial, fallas no encontradas y ausencia de tareas pendientes. Acredita la presentación de un resumen con datos para ambos parques después del aviso anterior; no se contrastaron esas cifras contra las fuentes a partir de la imagen. Se observan marcadores Markdown literales y el aviso final «Consulta realizada: no hay datos para esta selección», que no identifica el apartado vacío. No se infiere su origen solo por la captura ni se cambió la implementación. Continúan pendientes espera, rechazo de órdenes, recuperación de error y aislamiento visual entre usuarios.

### Corrección del aviso genérico de consulta vacía

A solicitud del usuario, se revisó el código que producía el aviso anterior. `ConsultaChatbotService` marcaba cualquier lista vacía de fallas o pendientes como `SinDatos`; `ChatbotClient.Fundamentar` anexaba ese mensaje al resumen aunque otras herramientas tuvieran resultados correctos. Esto explicaba la frase al final de una respuesta con potencia y energía.

Se ajustó únicamente la clasificación y redacción de los resultados en `SolarUY.Application/Chatbot/ConsultaChatbotService.cs`:

- Cero pendientes tras una consulta autorizada es `Correcto`, con el mensaje «No hay mantenimientos pendientes en el parque…».
- Cero fallas con paneles efectivamente consultados es `Correcto`, con el mensaje «No se encontraron paneles con fallas en el parque…».
- Si no hay paneles con datos para evaluar fallas, se mantiene `SinDatos`; no se afirma que no existan fallas.
- Las ausencias reales de estado o lecturas conservan `SinDatos` y ahora identifican el apartado y objetivo; generación diaria incluye el día y generación histórica incluye período y fecha de referencia. Cero energía válido conserva `Correcto`.

No se ocultaron advertencias desde el cliente ni se cambiaron herramientas, permisos, fuentes, integración con IA, cálculos o interfaz. El aviso ya no se produce por una lista vacía válida; los avisos por falta de lecturas o fuentes fallidas se siguen agregando. El Markdown literal observado en la captura queda fuera de esta corrección.

Se añadieron tres escenarios integrados en `ChatbotIntegrationTests.cs` (uno con dos variantes), utilizando Application, Agent Framework y MCP reales con las fronteras externas controladas. Comprueban respuesta sin aviso falso con fallas/pendientes vacíos, conservación de avisos ante ausencia de lecturas o caída de histórico y ausencia de equipos sin inferir salud. También se ejecutaron las verificaciones existentes de mantenimiento para los tres roles, cero/sin datos/fuente caída y resumen general.

Resultado sobre esta corrección: **9 casos de integración correctos y 9 de arquitectura correctos, 0 fallos, 0 omisiones**. Ambos comandos usaron `--no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:BaseOutputPath=bin/Req017Avisos/`; integración seleccionó únicamente los seis métodos relacionados mediante `--filter`. No se repitieron la suite completa ni las pruebas de proveedores reales, no se iniciaron servidores adicionales y no se alteraron los guardarraíles. Se actualizaron A1 de `specs.md` y la tarea 11.8 con la semántica comprobada.

La revisión visual de la respuesta corregida sigue pendiente. La instancia que ya estaba abierta debe reiniciarse con el código actualizado y comenzar una conversación nueva para observar el cambio; las respuestas anteriores no se reescriben.

### Nueva indisponibilidad del proveedor bajo la carga del equipo

El usuario aportó después una captura con «El asistente no está disponible». Ollama respondía en `/api/version` (0.40.16), pero no tenía modelos cargados. Su registro local mostró un HTTP 500 y `cudaMalloc failed` al reservar **2304 MiB de caché KV**, con contexto 16384 y lote 128. La solicitud había llegado al proveedor: no se atribuye este fallo al cambio de mensajes de listas vacías.

La consulta del equipo informó 15,72 GiB de RAM total, 2,61 GiB libre, 6,99 GiB de memoria virtual disponible y apenas **0,25 GiB libres en C:**. La GPU tenía unos 14 GiB libres, por lo que ese indicador por sí solo vuelve a ser insuficiente para acreditar capacidad de carga. WSL tenía aproximadamente 6,5 GiB privados. Estas cifras corresponden al diagnóstico posterior al fallo y no se presentan como una medición del pico exacto.

Se inventariaron exclusivamente las cuatro salidas `bin/Req017*` creadas durante este requerimiento dentro del workspace: suman unos **573 MiB**. No se eliminaron archivos, modelos, volúmenes ni datos; ese espacio sería insuficiente para recuperar un margen razonable. Se solicitó al usuario liberar al menos 5 GiB en C: antes de repetir una sola consulta. Ese margen es un prerrequisito operativo propuesto, no una garantía de que todas las cargas simultáneas quepan en memoria.

No se cambió código, configuración, modelo, caché ni contexto en este diagnóstico, ni se repitieron las suites o la carga del modelo con el disco casi lleno. Los resultados aprobados anteriormente se conservan con sus condiciones de ejecución; la disponibilidad bajo la carga actual queda pendiente en 12.6, además de la revisión visual de 12.1.

### Repetición después de liberar espacio

El usuario confirmó la liberación de espacio. La nueva comprobación informó **14,9 GiB libres en C:**, 2,17 GiB de RAM libre y 7,11 GiB de memoria virtual disponible. Ollama 0.40.16 estaba activo sin modelos cargados. Jenkins, SonarQube, Keycloak, MariaDB y phpMyAdmin permanecieron activos; no se detuvieron servicios para esta repetición.

Se ejecutó exclusivamente:

```powershell
powershell -NoProfile -File SolarUY.Integration.Tests/Verificar-Chatbot.ps1 -Ollama -SinCompilar -Salida Req017Avisos -BaseDePruebas solaruy_req007_test_req017_recuperacion -Filtro 'FullyQualifiedName~Chatbot_OllamaRealResumenDeHoyConMariaDbYSimuladorTcpAislado'
```

Resultado: **1 correcta, 0 fallos, 0 omisiones**. Se reutilizaron los binarios que incluyen la corrección de avisos. La prueba recorrió el modelo local real, Agent Framework, MCP, MariaDB aislada y un simulador TCP propio; comprobó ambos parques, sus cuatro apartados, rechazo de la orden, ausencia de cambios en los datos y comprobaciones de secretos en respuesta/registros. Se conservaron modelo `qwen3:8b`, contexto 16384, lote 128, mapeo y permanencia de 60 segundos.

El simulador se cerró al finalizar la prueba. Después se solicitó la descarga del modelo y `/api/ps` confirmó **cero modelos cargados**. No se compiló, no se modificó código/configuración y no se repitieron otras pruebas. Esta consulta acredita recuperación con el espacio y servicios indicados; no garantiza capacidad bajo cualquier carga simultánea ni reemplaza la inspección visual pendiente. Se completó 12.6 y permanece abierta 12.1.

### Saludos y cortesía sin exigir datos

El usuario mostró que «hola» recibía la advertencia de falta de evidencia. El control era correcto para afirmaciones operativas, pero también descartaba la cortesía del modelo cuando no había invocaciones a herramientas.

`ChatbotService` ahora reconoce mensajes completos de saludo, agradecimiento, despedida y ayuda con una lista cerrada normalizada (mayúsculas, tildes, espacios y puntuación). Devuelve respuestas definidas en servidor sin invocar IA ni herramientas; también funciona si el proveedor está caído y evita cargar Ollama para un saludo. Ejemplos verificados: «hola», «¡HÓLA!», «buenos días», «Hola, ¿cómo estás?», «muchas gracias», «hasta luego» y «¿Qué podés hacer?».

La rama local sigue dentro del turno autenticado, conserva revalidación de identidad y el historial del circuito. Solo el texto local conocido puede ser correcto sin consultas; la respuesta arbitraria del proveedor mantiene la exigencia de evidencia. Los mensajes completos con pregunta operativa, como «Hola, ¿cuánta energía generó el Parque norte?», continúan por Agent Framework/MCP. Las órdenes se rechazan antes de clasificar cortesía, incluso cuando empiezan con «hola» o «gracias». No se agregó conversación general libre ni se cambiaron permisos, herramientas, modelo o configuración.

Archivos de código/pruebas de este ajuste:

- `SolarUY.Application/Chatbot/ChatbotService.cs`: clasificación de cortesía y reutilización de normalización, con los controles de identidad e historial existentes.
- `SolarUY.Integration.Tests/ChatbotCortesiaIntegrationTests.cs`: 16 casos integrados de cortesía, proveedor caído, historial, los tres roles, identidad inválida/cambiada y consulta posterior/mixta por MCP.
- `SolarUY.Integration.Tests/ChatbotSeguridadIntegrationTests.cs`: dos ejemplos adicionales de órdenes precedidas por cortesía; se conservaron las comprobaciones originales.

Se actualizaron `requirements.md`, A1 de `specs.md`, la tarea 5.7 y los documentos de resultados. Verificaciones ejecutadas secuencialmente con `--no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:BaseOutputPath=bin/Req017Cortesia/`:

- `CodeQuality.Tests`: **9 correctas, 0 fallos, 0 omisiones**, guardarraíles sin modificar.
- Integración filtrada por `ChatbotCortesiaIntegrationTests`, `Chat_RechazaOrdenesSinInvocarModeloNiFuentesDeEscritura`, `Chat_DescartaRespuestaSinEvidenciaDelTurno` y `Chat_CambioDeIdentidadDescartaRespuestaEHistorialAnterior`: **26 correctas, 0 fallos, 0 omisiones**. Las consultas operativas de los escenarios recorrieron MCP real con proveedor externo controlado.

No se repitió la suite completa ni se invocaron proveedores de IA reales o se iniciaron servidores adicionales. La instancia abierta debe cargar el código actualizado para probar «hola» en el navegador; esa comprobación visual posterior no se declara realizada.

### Variantes de ayuda y respuesta diferenciada

La siguiente captura del usuario confirmó que «hola» y «como estas?» recibían la cortesía local, pero repetían la misma presentación y «hola que podemos hacer hoy?» seguía devolviendo falta de evidencia. Se ajustó `ResponderCortesia` en `ChatbotService.cs`: ahora «cómo estás», «qué tal» y expresiones equivalentes reciben una respuesta propia, y las preguntas de capacidades aceptan variantes acotadas con saludo inicial, voseo/tuteo, plural, «hoy» y «en SolarUY». El texto de ayuda propone consultas posibles sin afirmar estados ni cifras actuales.

`EsPreguntaDeAyuda` valida la frase completa después de retirar únicamente el saludo reconocido. Los mensajes que añaden un objetivo o pregunta operativa continúan por Agent Framework/MCP. Se mantuvieron las validaciones y el proveedor existente; no se habilitó texto arbitrario del modelo sin evidencia. No se cambió la interfaz ni se incorporaron paquetes.

Se amplió `ChatbotCortesiaIntegrationTests.cs` con la secuencia exacta de la captura, saludos con varias palabras, variantes de capacidades y preguntas que agregan generación de un parque. También se comprobó que una respuesta inventada del modelo en una consulta mixta sigue descartándose. El mismo filtro de integración de la sección anterior pasó con **40 correctas, 0 fallos, 0 omisiones**; arquitectura pasó con **9 correctas, 0 fallos, 0 omisiones** sobre el código final. Salida `bin/Req017Cortesia/`, comandos secuenciales con las mismas opciones y sin repetir la suite completa ni cargar Ollama. Se actualizaron A1, 5.7 y el resumen; la comprobación visual de esta ampliación sigue pendiente.

### Conservar la conversación al navegar

El usuario reportó que cambiar de apartado y volver dejaba el chat vacío. `Chatbot.razor` llamaba a `ReiniciarAsync` tanto al inicializarse como al destruirse, y la lista visible solo existía en el componente. Las rutas interactivas ya comparten el circuito y su servicio scoped, por lo que no se agregó almacenamiento persistente ni se cambió la navegación global.

Cambios de esta corrección:

- `ContratosChatbot.cs`: se añadió `IChatbotService.ObtenerConversacionAsync` para recuperar una copia de la conversación visible.
- `ChatbotService.cs`: conserva intercambios completados en memoria del circuito, incluidos saludos, respuestas, rechazos de órdenes y errores controlados. El contexto enviado al modelo mantiene su selección anterior. La lectura exige identidad y roles válidos, limpia ambos historiales si cambian y entrega una copia para impedir modificaciones desde el consumidor. Reiniciar y denegar identidad también descartan ambos historiales.
- `Chatbot.razor`: restaura mensajes al inicializarse. Destruir la vista cancela la solicitud y desuscribe eventos, con disposición idempotente; conserva mensajes completados y evita restaurar una lectura obsoleta si cambia la identidad durante la inicialización. «Nueva conversación» sigue limpiando el historial. Una solicitud cancelada al salir no se incorpora al registro.
- `ChatbotPanelIntegrationTests.cs`: escenarios nuevos con componentes reales, el servicio real de identidad de Presentation, Application y MCP, MariaDB aislada y proveedor controlado. Se simula destrucción del panel, render de «Perfil y permisos» y creación de otro panel usando el mismo ámbito; se contrastan los ocho mensajes (incluidos error/rechazo) y el HTML escapado antes de reiniciar. También se cubren tres cambios de identidad/permisos, dos circuitos separados del mismo usuario, copias independientes y cancelación al destruir la vista.

Verificaciones, ejecutadas secuencialmente con salida `bin/Req017Conversacion/`:

| Verificación | Resultado |
| --- | --- |
| `CodeQuality.Tests`, código final | **9 correctas, 0 fallos, 0 omisiones**; guardarraíles sin cambios. |
| Contexto posterior a cortesía (tres roles), cambio de identidad e historial, cancelación y liberación del turno | **5 correctas, 0 fallos, 0 omisiones**; límites externos controlados y consultas operativas por MCP real. |
| Reapertura/reinicio, identidad/permisos, circuitos, copia del historial, cancelación del panel y envío/render seguro existente | **7 correctas, 0 fallos, 0 omisiones**, mediante `Verificar-Chatbot.ps1 -SinCompilar -Salida Req017Conversacion -BaseDePruebas solaruy_req007_test_req017_conversacion` y filtro de los cinco métodos relacionados de `ChatbotPanelIntegrationTests`. |

No se repitieron la suite completa ni las llamadas de IA real y no se iniciaron hosts HTTP adicionales; el render integrado se ejecutó sin iniciar Kestrel. Solo se modificaron los tres archivos de producción indicados, la prueba del panel y los documentos del requerimiento. La conservación cubre navegación interna en el mismo circuito; una recarga completa, otro circuito o un reinicio del servidor empieza una conversación nueva. El recorrido manual posterior en navegador sigue pendiente y no se marca la etapa 12 como completa.

### Continuidad y peticiones compuestas

Se reprodujo con Ollama real la secuencia de la captura en una base/simulador aislados: «¿Qué parques hay?», «agregale mas informacion» y «dime cuantos parques hay y detalla la informacion de cada uno?». El primer intento falló en la ampliación con `SinDatos`: el historial llegaba al modelo, pero no se invocaban herramientas del turno. Reforzar instrucciones y permitir un intento correctivo mejoró esa parte, pero la primera comprobación compuesta todavía detectó apartados omitidos. Esos fallos no se declararon aprobados.

Se incorporó planificación semántica acotada antes de la redacción, reutilizando el mismo modelo y Agent Framework. `PlanificacionChatbot` devuelve únicamente un JSON de acción y referencias de parques, con esquema cerrado y validación adicional del backend. El plan usa los últimos doce mensajes para identificar antecedentes; no los convierte en evidencia actual. Se reconocen listado/cantidad, detalle general, equipos/nodos/paneles, selección normal de herramientas y necesidad de aclaración. Los nombres o Guid se resuelven contra un listado MCP nuevo; una referencia inexistente o ambigua pide precisión. No se agregaron expresiones regulares para las frases de la captura.

El detalle general ejecuta, secuencialmente por MCP, las cuatro consultas existentes para cada parque seleccionado. El estado de equipos consulta solo ese apartado por parque. La cantidad explícita se obtiene del tamaño del listado MCP autorizado. Las consultas específicas conservan el agente con las seis herramientas, que pueden proponerse juntas pero se ejecutan sin concurrencia. Si el agente no consulta nada, hay un solo intento correctivo dentro del mismo tiempo máximo; se mantiene el rechazo de afirmaciones sin evidencia. Una aclaración definida en servidor usa `RequiereAclaracion` y se conserva en el historial para interpretar la respuesta siguiente.

Cambios de código/pruebas:

- `SolarUY.Infrastructure/Chatbot/PlanificacionChatbot.cs`: planificación mediante `ChatClientAgent`, JSON Schema y validación de estructura, acciones y referencias.
- `ChatbotClient.cs`: ejecución del plan sobre las funciones MCP existentes, conteo autorizado, aclaración segura, instrucciones de continuidad/completitud y un intento correctivo acotado.
- `OllamaChatClient.cs`: traducción de `ChatOptions.ResponseFormat` a `format` de la API nativa para las salidas estructuradas. Sin otro paquete, versión, modelo o cambio de contexto.
- `CatalogoMcpChatbot.cs`: descripciones más precisas para cantidad de parques, detalle y conteo de nodos/paneles. Catálogo de seis nombres/esquemas de argumentos conservado.
- `ContratosChatbot.cs` y `ChatbotService.cs`: estado neutral de aclaración y conservación de su intercambio, con autenticación/aislamiento existentes.
- `ChatbotPlanificacionIntegrationTests.cs`: 14 casos nuevos de contexto, planes, alcance, permisos, ambigüedad, aclaración y reintento sin evidencia.
- `ChatbotEscenario.cs` y `ChatbotProveedorIntegrationTests.cs`: frontera controlada que admite el paso JSON sin sustituir Application ni MCP; el adaptador HTTP verifica también ese formato.
- `ChatbotMariaDbIntegrationTests.cs`: escenario real de tres turnos, reutilizando la preparación aislada existente; verifica cuatro apartados de ambos parques por identidad, nombres, conteo exacto, rechazo de orden, secretos y datos sin cambios.

Se actualizaron requisitos, A1, verificación, tarea 7.9 y resumen. Se consultaron las [salidas estructuradas oficiales de Ollama](https://docs.ollama.com/capabilities/structured-outputs), la [API de la versión 0.40.16](https://github.com/ollama/ollama/blob/v0.40.16/api/types.go) y la documentación XML de los paquetes .NET 8 ya instalados. La API nativa no expone `tool_choice`; no se envió una opción inexistente. La planificación usa `ResponseFormat.ForJsonSchema` y la ejecución mantiene Agent Framework/MCP reales.

Verificación final, secuencial, salida `bin/Req017Dialogo/`:

| Comprobación | Resultado |
| --- | --- |
| Arquitectura, código final | **9 correctas, 0 fallos, 0 omisiones**, guardarraíles sin cambios. |
| Planificación, proveedor HTTP controlado, catálogo, seguridad, contexto con los tres roles y consultas existentes | **63 correctas, 0 fallos, 0 omisiones**. Filtro de las clases `ChatbotPlanificacionIntegrationTests`, `ChatbotProveedorIntegrationTests`, `ChatbotCatalogoIntegrationTests`, `ChatbotSeguridadIntegrationTests`, `ChatbotIntegrationTests` y el método `Chat_CortesiaConservaContextoYConsultaPosteriorPorMcp`. |
| `Chatbot_OllamaRealContinuaConversacionYDetallaCadaParque` | **1 correcta, 0 fallos, 0 omisiones** sobre la versión final; la misma conversación también pasó antes de reforzar las comprobaciones de cobertura por parque y cantidad exacta. |

Reproducción del caso real final:

```powershell
powershell -NoProfile -File SolarUY.Integration.Tests/Verificar-Chatbot.ps1 -Ollama -Salida Req017Dialogo -BaseDePruebas solaruy_req007_test_req017_dialogo -Filtro 'FullyQualifiedName~Chatbot_OllamaRealContinuaConversacionYDetallaCadaParque'
```

El equipo tenía 12,12 GiB libres en C: antes de estas pruebas. No se detuvieron servicios de CI ni se ejecutó la suite completa. El simulador aislado terminó con el caso real y el modelo se descargó después; `/api/ps` confirmó cero modelos cargados. OpenAI no se invocó ni se consumieron sus créditos. El contexto de 16384 sigue siendo un límite: se verificó esta conversación y los escenarios indicados, no una conversación ilimitada o cualquier petición compleja. El recorrido visual posterior a estos cambios sigue pendiente en 12.1.

## Configuración de Ollama en Docker

Se corrigió el endpoint de Compose: 127.0.0.1 dentro de la web no alcanzaba Ollama del host. El permiso `Chatbot:Ollama:PermitirHostDocker` admite exactamente host.docker.internal, además de loopback, y conserva rechazo de servidores arbitrarios, credenciales y rutas no válidas. Ollama debe escuchar en una interfaz accesible desde Docker; el README explica la configuración.

Los ocho escenarios de proveedor/configuración aprobaron. La suite general usa proveedores controlados; las cinco pruebas que requieren modelos o proveedores reales permanecen omitidas. Se ejercitó además el servidor temporal de chatbot y su rechazo HTTP 401 sin identidad. No se declara un nuevo recorrido visual ni una conversación con Ollama real.
