# Tareas: ABM de paneles y asignación a nodos (req18)

Fuente de alcance técnico: `specs.md`. Todas las tareas permanecen pendientes; las decisiones C1--C5 deben resolverse antes de implementar el comportamiento que condicionan.

## 1. Aclaraciones

- [x] 1.1 Resolver si `CantidadPanelesPrevista` es máximo o cantidad exacta y cómo se interpreta `null`. (C1)
- [x] 1.2 Confirmar tipos, formatos, validaciones y transiciones de estado del panel. (C2)
- [x] 1.3 Resolver si se permite reasignar un panel y sus reglas para nodo origen, destino e historial. (C3)
- [x] 1.4 Definir la política de baja ante tareas de mantenimiento y lecturas existentes. (C4)
- [x] 1.5 Confirmar la matriz de roles de consulta si no puede reutilizarse la vigente. (C5)
- [x] 1.6 Incorporar las decisiones aprobadas C1--C5 en `specs.md` antes de iniciar los cambios de código.

## 2. Domain

- [x] 2.1 Implementar o completar `Panel` con identidad, datos y asociación obligatoria a `Nodo`, encapsulando las validaciones aprobadas. (D1)
- [x] 2.2 Implementar los métodos de actualización y los estados/transiciones aprobados. (D1)
- [x] 2.3 Aplicar la regla aprobada de cantidades y garantizar rechazo sin cambios parciales; incorporar reasignación solo si C3 la autoriza. (D2)
- [x] 2.4 Implementar el comportamiento de baja aprobado para las relaciones del panel. (D2)
- [x] 2.5 Crear o ampliar las pruebas xUnit de dominio para las reglas implementadas. (D3, V1)

## 3. Application

- [x] 3.1 Definir o completar DTOs, `IPanelService`, `PanelService` e `IPanelRepository` para los casos de uso especificados. (A1)
- [x] 3.2 Implementar los casos de uso de alta, obtención, listados, edición y baja con validación de existencia, resultados controlados y delegación al dominio. (A1)
- [x] 3.3 Aplicar autorización en Application antes de persistir: Administrador para escrituras y la matriz confirmada para consultas. (A2)

## 4. Infrastructure

- [x] 4.1 Implementar `PanelRepository`, su registro y el mapeo EF Core necesario para `Panel` y su relación con `Nodo`. (I1)
- [x] 4.2 Implementar la persistencia atómica y coordinación de operaciones concurrentes conforme a C1 y, si procede, C3. (I2)
- [x] 4.3 Crear una migración incremental únicamente si el esquema aprobado lo requiere, preservando datos y dependencias existentes. (I1)

## 5. Presentation

- [x] 5.1 Crear o completar el listado general y la consulta por nodo, mostrando el nodo asociado. (P1)
- [x] 5.2 Crear o completar formularios Radzen de alta y edición con los campos aprobados y selección de nodo. (P1)
- [x] 5.3 Incorporar confirmación de baja y mensajes de resultado, validación, inexistencia y conflicto. (P1)
- [x] 5.4 Proteger rutas, acciones y controles por rol, consumiendo únicamente Application. (P1, A2)

## 6. Verificación

- [x] 6.1 Verificar Domain y el flujo persistido de alta, consulta, traslado y baja lógica con conservación de lecturas y mantenimiento. Evidencia: PanelPersistenciaIntegrationTests.
- [x] 6.2 Verificar que Tecnico y Consulta leen y que sus escrituras son rechazadas sin cambios en MariaDB. Evidencia: PanelPersistenciaIntegrationTests.
- [x] 6.3 Ejecutar `dotnet test` sobre `CodeQuality.Tests` y corregir incumplimientos en el código, sin modificar los guardarraíles. (V4)
- [x] 6.4 Comprobar interfaz y, si corresponde, aplicación de la migración en MariaDB; registrar verificaciones no ejecutadas y su causa. (V5)

## 7. Cierre y documentación

- [x] 7. Crear o actualizar implementations.md en la misma carpeta con el detalle de los cambios realizados, los archivos tocados y el resultado de las pruebas. Crear o actualizar tambien resumen.md en esa carpeta con una sintesis breve y facil de leer de lo realizado para el requerimiento.
