# Especificaciones técnicas: Simulador TCP del inversor

## Contexto y alcance

SolarUY ya tiene una página `/simulador`, servicios de Application para consultar el inversor y validar roles, y un adaptador simulado dentro del proceso web. REQ013 agrega un extremo de simulación separado al que la aplicación se conecta por TCP/IP. Ese extremo representa el sensor y el inversor del esquema del profesor y genera potencia aleatoria. No se integra hardware real, ARM ni WebAssembly.

La interfaz continúa dependiendo de Application y el transporte TCP queda encapsulado en Infrastructure. El simulador debe ejecutarse como proceso de software independiente para que exista el límite de comunicación de red solicitado.

## Decisiones concretadas

- Se conserva la arquitectura .NET 8 existente y no se agregan bibliotecas externas de comunicación.
- El cliente TCP pertenece a `Infrastructure` y no se exponen sockets ni detalles de mensajes a Application o Presentation.
- El extremo simulado se inicia como un proceso ejecutable separado, de modo que SolarUY se comunique con él por TCP/IP incluso cuando ambos corran en el mismo equipo.
- El protocolo usa UTF-8 con JSON Lines: una solicitud JSON por conexión y una respuesta JSON terminada en salto de línea. La respuesta contiene éxito, resultado o mensaje de error. El cliente abre una conexión por operación.
- La lectura usa la topología real sincronizada desde MariaDB. Cada panel activo y sano genera entre el 90 % y el 100 % de su potencia nominal mediante variación aleatoria, independiente de la hora real. La potencia total del nodo es la suma de las lecturas de sus paneles.
- La aplicación obtiene del repositorio los nodos, parques, paneles, números de serie, estados y potencias nominales persistidos, y sincroniza ese inventario con el simulador por TCP antes de consultarlo. No se usa una topología predeterminada ni se modifica el inventario de MariaDB.
- La generación aleatoria por panel se limita a entre el 90 % y el 100 % de su potencia nominal registrada. Paneles inactivos/fallados y el inversor apagado generan cero.
- El estado de encendido/apagado es global al simulador; inicia encendido. Apagado fuerza a cero la potencia de todos los paneles. Las operaciones existentes de panel y volcado a red se conservan.
- El host, puerto y espera máxima se configuran en `InversorTcp:Host`, `InversorTcp:Port` e `InversorTcp:TimeoutSeconds`; por defecto son `127.0.0.1`, `5055` y 3 segundos. No son secretos.
- El simulador se inicia manualmente en desarrollo con `dotnet run --project SolarUY.InversorSimulador/SolarUY.InversorSimulador.csproj`; no se inicia automáticamente junto con la web.
- El estado del simulador vive mientras corre su proceso y no se guarda en MariaDB.

## Cambios por capa

### 1. Domain

No se requieren cambios en Domain: el simulador no persiste entidades del modelo ni introduce reglas sobre parques, nodos o paneles. El estado de conexión, el transporte y la producción de valores simulados pertenecen al adaptador externo.

### 2. Application

Mantener `IInversorClient` como puerto de consultas y órdenes; agregar consulta del estado global y comando de encendido/apagado. Las lecturas se mantienen por panel y nodo. `IInversorService` coordina los casos de uso y valida permisos mediante `IUsuarioActual`: Administrador puede operar; Técnico y Consulta solo leer. El DTO de consulta agrega el estado global del inversor.

Application no debe referenciar TCP, `System.Net.Sockets`, serialización de mensajes ni la implementación del simulador. Los errores de conexión y de respuesta deben traducirse a resultados/excepciones de aplicación que Presentation pueda manejar sin depender de Infrastructure.

### 3. Infrastructure

Registrar un cliente TCP que implemente `IInversorClient` en la aplicación web. El adaptador obtiene host y puerto desde configuración, usa JSON Lines, respeta `CancellationToken`, valida las respuestas y convierte errores de transporte o mensajes inválidos en una excepción de Application controlada.

Agregar el proyecto ejecutable .NET 8 `SolarUY.InversorSimulador`, que registra el simulador actual como motor local y hospeda un listener TCP en Infrastructure. Procesa las operaciones de `IInversorClient` mediante JSON Lines, incluida la sincronización del inventario enviado por la aplicación. Las lecturas son aleatorias por panel e independientes de la hora real. El estado global de encendido/apagado permanece coherente entre órdenes y consultas; apagado implica potencia cero. El simulador no se conecta a MariaDB ni requiere credenciales; la aplicación envía los datos actuales del inventario y los controles no los persisten.

### 4. Presentation

Actualizar la página existente `/simulador` para mostrar el estado y la potencia recibidos por el servicio de Application, indicar que son valores simulados y ofrecer las órdenes de encendido/apagado solo a Administrador. Técnico y Consulta conservan la consulta de solo lectura. La página maneja la indisponibilidad del simulador y respuestas inválidas con un mensaje controlado, sin mostrar datos obsoletos como si fueran actuales.

La interfaz no abre sockets, no implementa generación aleatoria y no decide autorizaciones por sí sola.

## Verificación prevista

Las pruebas permanentes de integración cubren el adaptador TCP contra el listener local: conexión, consulta, variación y límites de lecturas, órdenes y persistencia del estado mientras corre el simulador, apagado con potencia cero, desconexión y respuesta inválida. También comprueban autorización de Administrador, Técnico y Consulta en Presentation y Application. Se reutilizan las herramientas de prueba ya presentes en la solución y no se modifican los guardarraíles arquitectónicos.
