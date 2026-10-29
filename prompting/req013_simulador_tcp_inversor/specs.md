# Especificaciones técnicas: Simulador TCP del inversor

## Contexto y alcance

SolarUY ya tiene una página `/simulador`, servicios de Application para consultar el inversor y validar roles, y un adaptador simulado dentro del proceso web. REQ013 agrega un extremo de simulación separado al que la aplicación se conecta por TCP/IP. Ese extremo representa el sensor y el inversor del esquema del profesor y genera potencia aleatoria. No se integra hardware real, ARM ni WebAssembly.

La interfaz continúa dependiendo de Application y el transporte TCP queda encapsulado en Infrastructure. El simulador debe ejecutarse como proceso de software independiente para que exista el límite de comunicación de red solicitado.

## Decisiones y aclaraciones pendientes

- Se conserva la arquitectura .NET 8 existente y no se agregan bibliotecas externas de comunicación.
- El cliente TCP pertenece a `Infrastructure` y no se exponen sockets ni detalles de mensajes a Application o Presentation.
- El extremo simulado se inicia como un proceso ejecutable separado, de modo que SolarUY se comunique con él por TCP/IP incluso cuando ambos corran en el mismo equipo.
- La dirección y el puerto del simulador deben configurarse fuera del código fuente.
- Los requerimientos no definen el formato de mensajes TCP, el encuadre de cada mensaje, el rango numérico de potencia ni si la lectura es global al inversor o por panel. Se deben confirmar antes de cerrar el contrato de red y la generación aleatoria.
- Los requerimientos tampoco especifican si el simulador debe iniciarse manualmente o junto con el entorno local. Esa operación debe definirse antes de documentar el arranque para compañeros.

## Cambios por capa

### 1. Domain

No se requieren cambios en Domain: el simulador no persiste entidades del modelo ni introduce reglas sobre parques, nodos o paneles. El estado de conexión, el transporte y la producción de valores simulados pertenecen al adaptador externo.

### 2. Application

Mantener `IInversorClient` como puerto de consultas y órdenes. Ajustar sus contratos únicamente después de confirmar si las lecturas y el estado se representan por inversor o por panel. `IInversorService` sigue coordinando los casos de uso y validando permisos mediante `IUsuarioActual`: Administrador puede operar; Técnico y Consulta solo leer.

Application no debe referenciar TCP, `System.Net.Sockets`, serialización de mensajes ni la implementación del simulador. Los errores de conexión y de respuesta deben traducirse a resultados/excepciones de aplicación que Presentation pueda manejar sin depender de Infrastructure.

### 3. Infrastructure

Reemplazar el acceso local del adaptador por un cliente TCP que implemente `IInversorClient`. El adaptador obtiene host y puerto desde configuración, respeta `CancellationToken`, valida las respuestas y convierte errores de transporte o mensajes inválidos en errores controlados para Application.

Agregar un extremo de simulación ejecutable e independiente que escuche conexiones TCP, represente el sensor/inversor, genere las lecturas aleatorias dentro del rango confirmado y procese las órdenes de prender y apagar. El estado de encendido debe ser coherente entre órdenes y consultas; apagado implica potencia cero. El simulador no accede a MariaDB ni requiere credenciales de la aplicación.

El formato y el encuadre del protocolo, el rango de potencia y la estrategia de generación quedan pendientes de la aclaración inicial y no se fijan en esta especificación.

### 4. Presentation

Actualizar la página existente `/simulador` para mostrar el estado y la potencia recibidos por el servicio de Application, indicar que son valores simulados y ofrecer las órdenes de encendido/apagado solo a Administrador. Técnico y Consulta conservan la consulta de solo lectura. La página maneja la indisponibilidad del simulador y respuestas inválidas con un mensaje controlado, sin mostrar datos obsoletos como si fueran actuales.

La interfaz no abre sockets, no implementa generación aleatoria y no decide autorizaciones por sí sola.

## Verificación prevista

Las pruebas permanentes de integración cubren el adaptador TCP contra el simulador ejecutable: conexión, consulta, variación y límites de lecturas, órdenes y persistencia del estado mientras corre el simulador, apagado con potencia cero, desconexión y respuesta inválida. También comprueban autorización de Administrador, Técnico y Consulta en Presentation y Application. Se reutilizan las herramientas de prueba ya presentes en la solución y no se modifican los guardarraíles arquitectónicos.
