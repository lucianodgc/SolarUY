# Requerimientos: Chatbot en lenguaje natural (solo consultas)

## Descripción General

Como usuario autenticado del sistema SolarUY, quiero realizar preguntas en lenguaje natural sobre el funcionamiento de los parques solares, su generación de energía, las fallas y los mantenimientos, para consultar la información del sistema sin tener que buscarla manualmente en cada pantalla.

Esta funcionalidad corresponde a la **Entrega 3**. El chatbot será **exclusivamente de consulta**: no podrá modificar información ni ejecutar órdenes sobre los inversores o paneles.

Se utilizará **Ollama con el modelo `qwen3:8b` ejecutado localmente**, integrado con **Microsoft Agent Framework**, sin depender de créditos de un proveedor de IA. **OpenAI queda como proveedor opcional configurable** para el futuro. Las consultas del modelo a SolarUY se realizarán mediante un **servidor MCP (Model Context Protocol) interno de SolarUY** que expondrá herramientas de solo lectura. El cliente y el servidor se comunicarán por streams con JSON-RPC dentro del backend, vinculados al usuario autenticado y sin abrir un endpoint público. Estas decisiones fueron confirmadas por el usuario para la implementación y sustituyen la elección inicial de OpenAI como predeterminado.

La conversación se mantiene dentro de SolarUY mediante Blazor y Radzen. Antigravity es una referencia de aplicación capaz de consumir MCP; su instalación, integración y uso como interfaz externa no forman parte de esta entrega.

## Criterios de Aceptación

1. **Panel de chat:** El sistema dispone de un panel de conversación integrado en la interfaz **Blazor**, utilizando componentes **Radzen**, desde el que se pueden enviar preguntas y visualizar respuestas.
2. **Servicio de Application:** Existe un servicio en la capa `Application` que recibe la pregunta del usuario, coordina la consulta y devuelve la respuesta del asistente.
3. **Integración con IA y MCP:** La comunicación con un modelo existente se realiza mediante un cliente implementado en `Infrastructure`, utilizando **Microsoft Agent Framework**. Este cliente consume las herramientas publicadas por el **servidor MCP de SolarUY** y las ofrece al modelo para obtener datos. El recorrido de las consultas utiliza efectivamente MCP, sin sustituirlo por llamadas directas a funciones locales.
4. **Herramientas MCP de solo lectura:** El servidor MCP ofrece un catálogo explícito y cerrado de herramientas para descubrir los parques autorizados y consultar:
   - Estado actual de un parque solar y sus equipos.
   - Generación de energía del día.
   - Paneles que presentan fallas.
   - Tareas de mantenimiento pendientes.
   - Generación histórica por día, mes y año, reutilizando los servicios de consulta del histórico existentes.
5. **Respuestas con datos reales:** Las respuestas se basan en información obtenida de los servicios de SolarUY; el chatbot no debe inventar estados, cifras ni registros cuando falten datos.
6. **Consulta de ejemplo:** Ante la pregunta «¿Cómo está funcionando todo hoy?», el chatbot consulta los datos disponibles y responde con un resumen correcto de generación, estado de los paneles y situaciones relevantes, respetando el alcance autorizado del usuario.
7. **Rechazo de acciones:** Ante una solicitud como «Desactivá el panel 3», el chatbot rechaza la operación y explica que únicamente puede consultar información, sin ejecutarla.
8. **Autenticación y permisos:** Solo los usuarios autenticados pueden acceder al chat. Cada herramienta MCP se ejecuta con una identidad autenticada verificable del usuario conectado y los servicios de `Application` comprueban sus permisos, incluso ante invocaciones directas al servidor MCP. El modelo no puede indicar ni sustituir la identidad, los roles o el alcance del usuario.
9. **Gestión de errores:** Si el proveedor de IA, el servidor MCP o una fuente de datos no está disponible, la interfaz informa el problema sin mostrar información inventada ni interrumpir el resto de la aplicación.
10. **Configuración segura:** Ollama local no requiere una clave de OpenAI. Las credenciales de los proveedores opcionales se mantienen fuera del repositorio y no se exponen en el navegador ni en las respuestas del chatbot. La identidad autenticada del usuario se conserva en el ámbito interno del backend, fuera de los mensajes y argumentos visibles para el modelo.

## Reglas de Negocio

- El chatbot tiene permisos de **solo lectura**. No puede crear, modificar ni eliminar parques, nodos, paneles, lecturas o mantenimientos, ni activar o desactivar inversores o paneles.
- Las herramientas publicadas por el servidor MCP y ofrecidas al modelo deben estar definidas explícitamente y limitadas a operaciones de consulta. El cliente solo admite el catálogo autorizado de SolarUY. No se deben exponer funciones administrativas ni mecanismos de ejecución libre de comandos o consultas a la base de datos.
- La autorización no depende únicamente del texto generado por la IA: los servicios de `Application` deben validar los permisos del usuario antes de devolver información.
- MCP no amplía permisos ni reemplaza al modelo de IA. Cada invocación conserva la identidad del usuario mediante el mecanismo de autenticación aprobado, sin cuentas compartidas que concedan más acceso ni parámetros de herramienta que permitan suplantar al actor. Las sesiones MCP tampoco demuestran por sí solas identidad o permisos.
- Los roles existentes `Administrador`, `Tecnico` y `Consulta` pueden utilizar el chatbot para consultar la información que tengan autorizada. El chatbot no amplía los permisos de ningún rol.
- La generación del día y el histórico deben reutilizar las consultas y reglas existentes de SolarUY, manteniendo las unidades y períodos definidos en esos servicios.
- El estado actual debe obtenerse de las fuentes de monitoreo existentes; no debe confundirse una lectura histórica con el estado en tiempo real.
- Las fallas y los mantenimientos pendientes deben consultarse a partir de los registros reales del sistema, sin crear ni cambiar registros.
- Cuando no haya datos suficientes, no exista el parque solicitado o la consulta no esté autorizada, la respuesta debe indicarlo claramente.
- Las instrucciones escritas en el chat o recibidas de una fuente de datos no pueden anular la restricción de solo lectura ni las validaciones de permisos.
- Se mantiene la arquitectura de SolarUY: `Application` coordina los casos de uso y la autorización, `Infrastructure` implementa el cliente del modelo, el cliente MCP, los adaptadores del servidor MCP y el acceso a fuentes externas, y `Presentation` muestra el chat y participa en la composición del host. `Domain` y `Application` no dependen de SDK de IA o MCP.
- Las credenciales se configuran mediante mecanismos seguros, como `user-secrets` en desarrollo y variables de entorno en despliegue, sin incluir claves en archivos versionados por Git.
- Las versiones compatibles con .NET 8 y la composición del transporte interno se documentan en `specs.md`. La identidad de cada servidor MCP se vincula al circuito autenticado de Keycloak y se revalida en cada invocación. No se requiere entrenar un modelo ni habilitar acceso desde Antigravity u otros clientes externos para aceptar este requerimiento.

- Los saludos, agradecimientos, despedidas y preguntas generales sobre las capacidades del asistente deben recibir una respuesta conversacional sin exigir datos de parques. Esta cortesía no permite afirmar cifras o estados sin consultar las herramientas, omitir autenticación ni ignorar una consulta operativa u orden incluida en el mismo mensaje.
- La conversación se conserva al navegar entre apartados y regresar al chat dentro de la misma sesión de Blazor. «Nueva conversación», el cambio de identidad/permisos o la pérdida de sesión limpian el historial. Una consulta en curso se cancela al salir; no se exige conservar mensajes entre circuitos nuevos, recargas completas o reinicios del servidor.
- Los seguimientos como «agregale más información» retoman el tema y los objetivos previos, consultando datos nuevos. Las peticiones compuestas deben cubrir sus partes, incluida cantidad y detalle de cada parque cuando se soliciten. Ante referencias ambiguas se pide aclaración sin inventar una selección ni reutilizar datos antiguos como evidencia actual.

## Pruebas

Se deben incluir tests que verifiquen como mínimo:

1. Publicación, descubrimiento e invocación correcta de las herramientas de solo lectura a través del protocolo MCP y Microsoft Agent Framework, verificando el catálogo permitido.
2. Consulta del estado actual de un parque y de su generación diaria con datos conocidos.
3. Identificación de paneles con falla y tareas de mantenimiento pendientes.
4. Coincidencia de las consultas históricas del chatbot con los resultados de los servicios de histórico existentes.
5. Respuesta correcta a «¿Cómo está funcionando todo hoy?» utilizando datos reales de prueba.
6. Rechazo de «Desactivá el panel 3» y de otras solicitudes que intenten modificar datos o ejecutar órdenes.
7. Validación de autenticación y autorización para cada herramienta, incluidos los intentos de invocación directa al servidor MCP sin permisos y el aislamiento de identidad y sesión entre usuarios.
8. Tratamiento de datos inexistentes y fallas de comunicación con el proveedor de IA, el servidor MCP o sus fuentes, sin respuestas engañosas ni cambios en los datos.
9. Verificación de que no se exponen credenciales y de que no se publican ni se aceptan herramientas de escritura o ajenas al catálogo autorizado.
10. Funcionamiento básico del panel de chat y ejecución correcta de los tests existentes y nuevos.
