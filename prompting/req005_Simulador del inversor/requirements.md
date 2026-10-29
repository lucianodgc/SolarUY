# Requerimientos: Simulador de Inversor

## Descripción General

Como sistema SolarUY, quiero disponer de un simulador de inversor que implemente la interfaz `IInversorClient`, para poder obtener información simulada sobre el estado y la potencia de los paneles sin depender todavía de una API o dispositivo real.

El simulador debe representar de forma coherente el comportamiento básico de un inversor, permitiendo consultar paneles de un nodo y ejecutar acciones como activar o desactivar paneles y el volcado de energía a la red.

La implementación debe permitir que, en el futuro, el simulador pueda ser reemplazado por una integración real sin modificar la lógica de negocio de la aplicación.

## Criterios de Aceptación

### 1. Interfaz IInversorClient

- Debe existir una interfaz `IInversorClient` definida en la capa `Application`.
- La interfaz debe permitir, como mínimo:
  - Obtener el estado de los paneles de un nodo.
  - Obtener la potencia generada por los paneles.
  - Activar un panel.
  - Desactivar un panel.
  - Activar el volcado de energía a la red.
  - Desactivar el volcado de energía a la red.

### 2. Implementación del Simulador

- Debe existir una implementación simulada de `IInversorClient`.
- El simulador debe encontrarse en la capa `Infrastructure`.
- El simulador debe devolver información coherente para cada panel y nodo.
- El estado generado por el simulador debe poder consultarse mediante la interfaz definida en `Application`.

### 3. Simulación de Potencia

- La potencia generada por los paneles debe variar aleatoriamente dentro de límites válidos, sin depender de la hora real del equipo.
- Un panel activo y sano debe poder generar potencia a cualquier hora.
- La potencia debe ser cero cuando el inversor está apagado o el panel está desactivado o fallado.

### 4. Estado de los Paneles

- Cada panel debe disponer de un estado de salud simulado.
- El simulador debe poder generar fallas de paneles de forma configurable.
- El estado devuelto debe mantenerse coherente con las acciones realizadas sobre el panel.

### 5. Activación y Desactivación

- El sistema debe permitir activar y desactivar un panel mediante `IInversorClient`.
- Al desactivar un panel, las consultas posteriores deben reflejar que se encuentra desactivado.
- Un panel desactivado no debe reportar generación de energía.
- Al activar nuevamente el panel, este debe volver a participar de la simulación.

### 6. Volcado de Energía a la Red

- El sistema debe permitir activar y desactivar el volcado de energía a la red.
- El simulador debe mantener el estado actual del volcado.
- Las consultas posteriores deben reflejar el último estado configurado.

### 7. Desacoplamiento de la Infraestructura

- El resto de la aplicación debe depender de `IInversorClient` y no directamente de la implementación del simulador.
- Reemplazar el simulador por una implementación que utilice una API real no debe requerir cambios en las capas `Domain` o `Application`.

## Reglas de Negocio

- Un panel desactivado no puede generar potencia.
- La potencia simulada debe mantenerse dentro de valores válidos.
- El estado de un panel debe ser consistente entre las consultas y las órdenes ejecutadas sobre él.
- Las fallas simuladas deben poder configurarse para permitir pruebas controladas.
- Las operaciones sobre un panel deben afectar únicamente al panel correspondiente.

## Ampliación de interfaz solicitada

- Pantalla `/simulador` con selección de nodos y paneles del inventario persistido, potencia del nodo y estado del volcado a red.
- Administrador puede activar/desactivar paneles y el volcado. Técnico y Consulta solo consultan; esta decisión fue confirmada por el usuario.
- Los controles actualizan el estado simulado visible después de la operación y previenen envíos repetidos. La pantalla informa que las lecturas son simuladas; los controles no modifican el inventario persistido.

## Pruebas

Se deben incluir tests de la lógica del simulador que verifiquen como mínimo:

- Obtención de información de paneles por nodo.
- Generación aleatoria de potencia independiente de la hora del día.
- Activación de un panel.
- Desactivación de un panel.
- Un panel desactivado no genera potencia.
- Cambio del estado de volcado a la red.
- Comportamiento ante fallas simuladas.
