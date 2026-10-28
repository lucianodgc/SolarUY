# Tareas: Autorización por roles para la gestión solar

## 1. Domain

- [x] 1.1 Confirmar que el alcance no requiere cambios en entidades ni reglas del dominio; no introducir dependencias de autorización en Domain.

## 2. Application

- [x] 2.1 Revisar las operaciones de consulta de parques y permitir su uso de solo lectura a Administrador, Técnico y Consulta, manteniendo denegadas las escrituras a los roles no administrativos.
- [x] 2.2 Localizar las operaciones y servicios existentes de nodos y paneles, y aplicar las comprobaciones de rol de la especificación a cada operación CRUD y consulta. Registrar como clarificación cualquier operación requerida que no tenga un punto de entrada existente, sin inventar su contrato funcional. No hay servicios ni páginas CRUD de nodos/paneles en la solución actual; se registra esta limitación en `implementations.md`.
- [x] 2.3 Revisar las operaciones de mantenimiento y confirmar que cada lectura y modificación aplica en Application los permisos y alcances definidos por `req004_Mantenimiento_de_paneles`.
- [x] 2.4 Revisar las operaciones del inversor: permitir consulta a los tres roles y restringir toda orden al rol Administrador antes de invocar el cliente del inversor.
- [x] 2.5 Añadir o actualizar pruebas de Application que cubran los permisos y denegaciones por operación y rol, incluidos usuario no autenticado y usuario sin rol, y que comprueben que un rechazo no produce persistencia ni llamadas externas.

## 3. Infrastructure

- [x] 3.1 Verificar que la implementación se apoya en `IUsuarioActual` desde Application y no añade lógica de autorización, dependencia de claims ni cambios de esquema a Infrastructure.

## 4. Presentation

- [x] 4.1 Alinear los atributos `[Authorize(Roles = "...")]` de las páginas con la matriz de permisos, incluyendo las pantallas de parques, nodos, paneles, mantenimiento e inversor que estén presentes.
- [x] 4.2 Alinear los `AuthorizeView` de navegación y acciones con la matriz, manteniendo la vista existente de acceso denegado para usuarios autenticados sin permiso.
- [x] 4.3 Añadir o actualizar pruebas existentes de acceso a rutas y visibilidad/denegación por cada rol.

## 5. Verificación y cierre

- [x] 5.1 Ejecutar las verificaciones existentes contempladas en `specs.md` y registrar sus resultados y cualquier verificación no ejecutada.
- [x] 5.2 Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
