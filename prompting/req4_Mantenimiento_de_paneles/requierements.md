# Requerimientos: Mantenimiento de paneles

## Objetivo

Gestionar tareas de mantenimiento de paneles existentes: programar, asignar, seguir estados y registrar su realización.

## Datos

Cada tarea contiene panel asociado, tipo de mantenimiento como texto, descripción obligatoria que representa las observaciones, fecha programada UTC, responsable obligatorio, estado y fecha de realización opcional hasta completar.

Se crea siempre Pendiente. Estados: Pendiente, EnCurso, Completada y Cancelada.

ResponsableId es el sub del usuario de Keycloak, de tipo string y sin FK o tabla paralela de usuarios/técnicos. El modelo de Panel/Nodo y sus atributos se toman del MER existente.

## Roles y operaciones

Administrador:
- Crear tareas asociadas a panel existente.
- Asignar y reasignar a un técnico habilitado.
- Consultar todas las tareas.
- Editar tipo, descripción, fecha programada y responsable exclusivamente en Pendiente o EnCurso.
- Iniciar, completar y cancelar según las transiciones permitidas.

Tecnico:
- Consultar únicamente las tareas asignadas.
- Iniciar, completar y cancelar únicamente sus tareas.
- No crear tareas, editar datos ni reasignar responsables.

Los usuarios sin esos roles, incluido Consulta por sí solo, no acceden al mantenimiento. Los permisos se verifican en los casos de uso, también en accesos por identificador y filtros enviados por el usuario.

## Ciclo de vida y reglas

- Pendiente puede pasar a EnCurso.
- EnCurso puede pasar a Completada y registra FechaRealizacion UTC.
- Pendiente o EnCurso pueden pasar a Cancelada por administrador o técnico asignado.
- Completadas y canceladas no se editan, reasignan ni reactivan.
- No cambiar el panel de una tarea ni habilitar transiciones adicionales.
- La fecha programada es un instante DateTime UTC válido. Se permiten fechas pasadas.
- Vencida significa fecha estrictamente anterior a la referencia UTC y estado Pendiente o EnCurso; la igualdad no es vencimiento.
- Tipo, descripción y responsable son obligatorios. No añadir catálogos o límites funcionales de texto sin respaldo.
- Ninguna operación crea paneles o usuarios automáticamente.
- Las escrituras desactualizadas deben rechazarse si la tarea fue completada, cancelada, reasignada o modificada concurrentemente.

## Consultas

Permitir filtros combinables por estado, vencimiento, técnico y panel, respetando siempre el alcance del usuario autenticado. Técnicos solo ven sus tareas y opciones de panel asociadas a ellas.

## Fuente de técnicos y decisiones tecnológicas aprobadas

Keycloak es la fuente de verdad. Implementar ITecnicoDirectory en Application para listar usuarios habilitados con rol efectivo Tecnico y validar que ResponsableId exista, esté habilitado y tenga ese rol.

La implementación de Infrastructure consulta Keycloak Admin REST API con client_credentials y cliente confidencial/service account. Permisos mínimos de lectura view-users/query-users de realm-management o equivalentes. No usar credenciales de un administrador humano ni otorgar permisos de escritura.

El ClientSecret va en user-secrets para desarrollo y variables de entorno para despliegue. No guardar secretos en Git. El resto de la aplicación depende únicamente de la abstracción de Application.

Usar tecnologías y arquitectura autorizadas del proyecto. Implementar las dependencias mínimas Nodo/Panel del MER necesarias para mantenimiento; altas, inversores, lecturas y el resto de req003/req005 quedan fuera de este alcance.

## Pruebas y entrega

Incluir pruebas de dominio de creación, datos requeridos, transiciones, finalización, cancelación, edición, inmutabilidad y vencimiento UTC, junto con las de los constructores nuevos de Nodo/Panel.

Verificar arquitectura, migración incremental, persistencia, directorio de Keycloak, permisos y protección ante escrituras concurrentes. Registrar cambios y resultados, indicando cualquier comprobación visual u operativa no ejecutada.
