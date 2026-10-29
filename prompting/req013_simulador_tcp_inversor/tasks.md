# Tareas de implementación: Simulador TCP del inversor

## 1. Aclaraciones previas

- [ ] 1.1 Definir el formato, el encuadre y los mensajes de consulta, respuesta, prender y apagar del protocolo TCP.
- [ ] 1.2 Confirmar si potencia y estado son globales al inversor o se informan por panel, junto con el rango y la unidad de potencia aleatoria.
- [ ] 1.3 Definir cómo se inicia el proceso simulador en desarrollo y qué host/puerto se usarán por defecto.

## 2. Application

- [ ] 2.1 Ajustar el contrato `IInversorClient` y los DTOs para representar el estado y lecturas confirmados en la aclaración 1.2.
- [ ] 2.2 Mantener los casos de uso de consulta y órdenes con autorización en Application: Administrador opera; Técnico y Consulta solo consultan.
- [ ] 2.3 Traducir desconexión y respuesta inválida a errores controlados de Application sin filtrar tipos de Infrastructure.

## 3. Infrastructure

- [ ] 3.1 Implementar el cliente TCP de `IInversorClient`, con host/puerto configurables, cancelación y validación de mensajes según 1.1 y 1.3.
- [ ] 3.2 Crear el proceso ejecutable del simulador remoto y su listener TCP usando el protocolo definido.
- [ ] 3.3 Generar valores aleatorios dentro del rango acordado y mantener consistentes estado y potencia después de cada orden.
- [ ] 3.4 Manejar desconexiones, mensajes inválidos y cancelación con errores acotados.
- [ ] 3.5 Registrar el cliente TCP en la composición de dependencias de SolarUY y configurar el ejecutable del simulador para desarrollo.

## 4. Presentation

- [ ] 4.1 Actualizar `/simulador` para mostrar lecturas y estado recibidos desde Application e indicar que son simulados.
- [ ] 4.2 Presentar los controles de prender/apagar únicamente a Administrador y mantener la autorización del servicio.
- [ ] 4.3 Mostrar errores controlados de comunicación o respuesta inválida y evitar presentar lecturas antiguas como actuales.

## 5. Verificación

- [ ] 5.1 Incorporar pruebas de integración para consulta TCP, aleatoriedad y límites, órdenes, estado apagado, desconexión y respuestas inválidas.
- [ ] 5.2 Verificar por rol las consultas y órdenes en Application y Presentation sin modificar los guardarraíles arquitectónicos.

## 6. Cierre y documentación

- [ ] 6. Crear el archivo `implementations.md` en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
