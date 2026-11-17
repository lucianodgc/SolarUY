# Requerimientos: Órdenes al inversor — activar/desactivar panel y volcado a la red

## Descripción General

Como Administrador del sistema SolarUY, quiero poder enviar órdenes a los inversores para activar o desactivar un panel y habilitar o deshabilitar el volcado de energía a la red, desde la pantalla de monitoreo, con confirmación previa y registro de auditoría.

Esta funcionalidad corresponde a la Entrega 3 y debe integrarse con el inventario de paneles e inversores, la autenticación mediante Keycloak, la recolección periódica de lecturas y el monitoreo en tiempo real existentes.

## Criterios de Aceptación

### 1. Órdenes disponibles

- Permitir activar un panel existente mediante un caso de uso de Application que invoque `IInversorClient`.
- Permitir desactivar un panel existente mediante un caso de uso de Application que invoque `IInversorClient`.
- Permitir activar el volcado de energía a la red mediante un caso de uso de Application que invoque `IInversorClient`.
- Permitir desactivar el volcado de energía a la red mediante un caso de uso de Application que invoque `IInversorClient`.
- No implementar un segundo mecanismo de comunicación con el inversor ni duplicar la lógica de sus órdenes.

### 2. Interfaz de monitoreo

- Incorporar controles para las cuatro operaciones en la pantalla de monitoreo correspondiente.
- Solicitar confirmación explícita antes de enviar cada orden, identificando la acción y su destino.
- Informar al usuario si la orden fue ejecutada, rechazada o falló; no mostrar éxito sin confirmación del resultado de la operación.
- Evitar envíos accidentales repetidos mientras una orden esté en curso, sin depender de este bloqueo visual como medida de seguridad.

### 3. Seguridad y autorización

- Solo los usuarios autenticados con el rol efectivo `Administrador` pueden emitir órdenes.
- Los roles `Tecnico` y `Consulta`, por sí solos, no pueden ejecutar ninguna orden.
- Verificar la autorización en los casos de uso de Application, independientemente de que la interfaz oculte los botones.
- Rechazar solicitudes directas al servicio hechas por usuarios sin permisos.
- Obtener la identidad del usuario autenticado de los claims emitidos por Keycloak; no confiar en identificadores de usuario suministrados desde el navegador.

### 4. Auditoría

- Registrar cada intento de orden con el usuario responsable (identificador `sub` de Keycloak), la operación solicitada, el destino correspondiente y la fecha y hora UTC.
- Registrar el resultado conocido de la ejecución (por ejemplo, confirmada, rechazada o fallida) y la información de error pertinente, sin guardar credenciales ni secretos.
- Conservar el registro de auditoría incluso cuando la operación falle, según el mecanismo de persistencia acordado.
- Evitar atribuir como ejecutada una orden cuyo resultado no haya sido confirmado.

### 5. Integración con recolección y monitoreo

- Cuando un panel se desactive correctamente en el inversor simulado, su siguiente medición debe reflejar una potencia generada de `0 W`.
- La pantalla de monitoreo debe actualizar la información a partir de las nuevas lecturas por el mecanismo SignalR existente, sin exigir una recarga manual.
- La activación de un panel no implica garantizar una potencia positiva: la generación depende de las condiciones de operación.
- Diferenciar una desactivación ordenada de una falla técnica, para no clasificar automáticamente como fallado a un panel únicamente por producir `0 W` tras una orden de desactivación.
- Mantener la compatibilidad con el despliegue de dos instancias detrás de Nginx; no introducir unilateralmente un mecanismo nuevo de distribución.

## Reglas de Negocio

- Una orden sobre un panel debe referirse a un panel existente y al inversor o nodo que corresponda según el inventario vigente.
- No crear paneles, nodos, inversores o usuarios automáticamente como consecuencia de enviar una orden.
- No considerar completada una orden si el inversor devuelve un error, rechaza la operación o no confirma su resultado.
- Manejar los errores de comunicación sin generar una confirmación falsa al usuario.
- Una operación repetida no debe generar estados contradictorios; la semántica exacta de reintentos y respuestas para estados ya alcanzados debe respetar el contrato de `IInversorClient` existente.
- Las operaciones de volcado a la red afectan al destino definido por el contrato vigente del inversor; no asumir un alcance por parque o por nodo sin verificar ese contrato.
- Usar fechas y horas en UTC para la auditoría.

## Restricciones Técnicas

- Mantener la arquitectura y las tecnologías aprobadas para SolarUY: Dominio, Application, Infrastructure y Presentation; .NET, EF Core, MariaDB y autenticación con Keycloak.
- Definir los casos de uso en Application; mantener la implementación de comunicación con el inversor detrás de `IInversorClient`.
- Guardar la auditoría mediante la infraestructura de persistencia existente o una ampliación incremental de esta.
- No introducir un nuevo proveedor de identidad, servicio de mensajería ni mecanismo de comunicación con el inversor sin aprobación.
- No almacenar contraseñas, tokens ni secretos de Keycloak en registros de auditoría.

## Pruebas y Entrega

- Tests de autorización para `Administrador`, `Tecnico`, `Consulta` y usuarios sin autenticar, incluyendo llamadas directas a los casos de uso.
- Tests de los cuatro casos de uso, verificando que invocan correctamente `IInversorClient` y manejan rechazos y fallos.
- Tests de auditoría: identidad, acción, destino, instante UTC y resultado, tanto en éxito como en error.
- Prueba de integración con el simulador: desactivar un panel, ejecutar la recolección y comprobar que el monitoreo refleja `0 W`.
- Comprobar que la activación y el volcado producen el cambio de estado correspondiente según las capacidades reales del simulador.
- Verificar la confirmación visual y la actualización de pantalla, documentando cualquier comprobación no ejecutada.
- Dejar en verde las pruebas afectadas y documentar migraciones incrementales y cambios realizados.

## Decisiones a Confirmar Antes de Implementar

- Alcance exacto de las órdenes de volcado a la red según la interfaz disponible en `IInversorClient`.
- Tratamiento de órdenes con resultado incierto por interrupciones de comunicación y política de reintentos.
- Modelo definitivo de persistencia de auditoría y estado de ejecución.
- Forma de distinguir, en la lógica de salud, un panel desactivado voluntariamente de uno con falla, de acuerdo con las reglas ya aprobadas para la Entrega 3.

Estas decisiones deben resolverse respetando los contratos y funcionalidades existentes, sin ampliar unilateralmente el alcance del requerimiento.
