# Requerimientos: Autorización por roles para la gestión solar

## Descripción general

Como usuario de SolarUY (Administrador, Técnico o Consulta), quiero que las operaciones disponibles dependan de mi rol para que la gestión de parques, nodos, paneles, inversores y mantenimiento quede restringida a los perfiles autorizados.

## Roles y permisos

### Administrador

- Puede crear, consultar, editar y eliminar parques, nodos y paneles (ABM).
- Puede emitir órdenes al inversor.

### Técnico

- Puede realizar las operaciones de mantenimiento que le correspondan.
- No puede crear, editar ni eliminar parques, nodos o paneles.
- No puede emitir órdenes al inversor.

### Consulta

- Puede consultar la información disponible en modo de solo lectura.
- No puede crear, editar ni eliminar parques, nodos o paneles, realizar mantenimiento ni emitir órdenes al inversor.

## Reglas de autorización

- Aplicar autorización por rol en la interfaz y en los endpoints o componentes de presentación que expongan las operaciones.
- Los casos de uso y servicios de Application deben verificar el rol y denegar las operaciones no permitidas; ocultar controles de interfaz no constituye autorización suficiente.
- La autorización debe aplicarse también a operaciones de lectura cuando el alcance del rol así lo limite y a accesos directos por identificador o parámetros suministrados por el usuario.
- Los usuarios sin el rol requerido deben recibir acceso denegado tanto desde la interfaz como al invocar directamente el servicio.
- Los roles utilizados son `Administrador`, `Tecnico` y `Consulta`.

## Criterios de aceptación

1. Un usuario que no tenga el rol Administrador no puede crear, editar ni eliminar parques, nodos o paneles desde la interfaz ni invocando los servicios de Application.
2. Solo Administrador puede emitir órdenes al inversor; la operación está protegida en la interfaz y en Application.
3. Técnico puede acceder a las operaciones de mantenimiento permitidas y no a operaciones administrativas.
4. Consulta puede consultar información en modo de solo lectura y no ejecutar operaciones de escritura.
5. Una operación no autorizada produce una respuesta de acceso denegado en la interfaz y una denegación equivalente en el servicio.
6. Hay pruebas que verifican los permisos y denegaciones para cada rol en las operaciones aplicables.

## Alcance

Este requisito cubre la autorización por roles para la gestión de parques, nodos y paneles, las órdenes al inversor, el mantenimiento y la consulta de solo lectura. Los detalles funcionales de mantenimiento y las reglas propias de las operaciones de inversor se rigen por sus requisitos específicos existentes; este documento define quién puede acceder a ellas.
