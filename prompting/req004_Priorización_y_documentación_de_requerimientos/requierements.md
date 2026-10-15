# Requerimientos: Mantenimiento de Paneles

## Descripción General

Como usuario del sistema SolarUY, quiero gestionar tareas de mantenimiento asociadas a los paneles solares, para poder programar, asignar, realizar seguimiento y registrar las tareas necesarias para mantener los paneles en condiciones adecuadas.

El sistema debe permitir crear tareas de mantenimiento, asignarlas a técnicos, modificar su estado y consultar las tareas pendientes, vencidas y asociadas a cada panel.

## Criterios de Aceptación

### 1. Registro de Tareas de Mantenimiento

- El sistema debe permitir crear una tarea de mantenimiento asociada a un panel.
- Cada tarea debe contener:
  - Panel asociado.
  - Tipo de mantenimiento.
  - Fecha programada.
  - Responsable.
  - Estado.
  - Observaciones.

- Los estados posibles de una tarea son:
  - `Pendiente`
  - `EnCurso`
  - `Completada`
  - `Cancelada`

### 2. Asignación de Tareas

- Un usuario con rol `Administrador` debe poder crear tareas de mantenimiento.
- El Administrador debe poder asignar una tarea a un técnico.
- El técnico asignado debe poder visualizar las tareas que tiene a su cargo.

### 3. Gestión de Estados

- Una tarea debe poder cambiar de estado durante su ciclo de vida.
- Una tarea pendiente puede pasar a `EnCurso`.
- Una tarea en curso puede pasar a `Completada`.
- Una tarea puede ser `Cancelada` cuando corresponda.
- Al completar una tarea debe quedar registrado su estado como `Completada`.

### 4. Consulta de Tareas

El sistema debe permitir consultar:

- Tareas pendientes.
- Tareas en curso.
- Tareas vencidas.
- Tareas asignadas a un técnico.
- Tareas asociadas a un panel específico.

Una tarea se considera vencida cuando su fecha programada ya pasó y todavía no fue completada ni cancelada.

### 5. Permisos por Rol

- **Administrador:**
  - Crear tareas.
  - Asignar tareas a técnicos.
  - Consultar las tareas de mantenimiento.
  - Gestionar tareas mientras no estén completadas.

- **Técnico:**
  - Consultar las tareas que tiene asignadas.
  - Actualizar el estado de sus tareas.
  - Completar las tareas asignadas.

## Reglas de Negocio

- Toda tarea de mantenimiento debe estar asociada a un panel existente.
- Una tarea debe tener una fecha programada.
- Una tarea debe tener un tipo de mantenimiento definido.
- Una tarea marcada como `Completada` no puede ser modificada posteriormente.
- Un técnico solamente puede gestionar las tareas que tiene asignadas.
- Las tareas vencidas deben poder identificarse a partir de su fecha programada y estado actual.

## Pruebas

Se deben incluir tests de dominio que verifiquen como mínimo:

- Creación correcta de una tarea de mantenimiento.
- Cambio de estado de una tarea.
- Finalización de una tarea.
- Imposibilidad de modificar una tarea completada.
- Identificación de tareas vencidas.