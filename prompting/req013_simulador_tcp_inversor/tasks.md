# Tareas de implementación: Simulador TCP del inversor

## 1. Aclaraciones previas

- [x] 1.1 Definir JSON Lines UTF-8 con una solicitud y respuesta JSON por conexión, con estado, resultado y error opcional.
- [x] 1.2 Mantener lecturas aleatorias por panel en watts, independientes de la hora real, con factor 0,90–1,00; el estado de encendido/apagado es global.
- [x] 1.3 Configurar host/puerto `127.0.0.1:5055` y arranque manual mediante `dotnet run` del proyecto simulador .NET 8.

## 2. Application

- [x] 2.1 Extender `IInversorClient` e `IInversorService` con consulta y orden del estado global de encendido.
- [x] 2.2 Agregar el estado global al DTO de consulta conservando la salida por nodo y panel.
- [x] 2.3 Mantener la autorización en Application: Administrador opera; Técnico y Consulta solo consultan.
- [x] 2.4 Definir una excepción de Application para comunicar indisponibilidad o respuesta inválida del simulador.

## 3. Infrastructure

- [x] 3.1 Implementar el cliente TCP JSON Lines para cada operación de `IInversorClient`, con configuración, cancelación y validación de respuestas.
- [x] 3.2 Implementar en el simulador el estado global de encendido, mantener el estado por panel/volcado y aplicar la variación aleatoria acordada.
- [x] 3.3 Crear el proceso ejecutable `SolarUY.InversorSimulador` y su listener TCP usando el motor simulado de Infrastructure.
- [x] 3.4 Manejar desconexiones, mensajes inválidos y cancelación con errores acotados.
- [x] 3.5 Registrar el cliente TCP en SolarUY y configurar host/puerto sin secretos ni valores codificados en el adaptador.
- [x] 3.6 Sincronizar por TCP el inventario de nodos y paneles leído desde la base de datos, incluyendo estado, número de serie y potencia nominal.

## 4. Presentation

- [x] 4.1 Actualizar `/simulador` para mostrar el estado global y las lecturas recibidas desde Application, indicando que son simulados.
- [x] 4.2 Presentar los controles de prender/apagar únicamente a Administrador y mantener las operaciones de panel/volcado existentes.
- [x] 4.3 Mostrar errores controlados de comunicación o respuesta inválida y evitar presentar lecturas antiguas como actuales.

## 5. Verificación

- [x] 5.1 Incorporar pruebas de integración para consulta TCP, aleatoriedad y límites, órdenes, estado apagado, desconexión y respuestas inválidas.
- [x] 5.2 Verificar por rol las consultas y órdenes en Application y Presentation sin modificar los guardarraíles arquitectónicos.

## 6. Cierre y documentación

- [x] 6. Crear el archivo `implementations.md` en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
