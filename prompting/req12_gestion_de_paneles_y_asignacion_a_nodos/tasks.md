# Tareas: gestión de paneles y asignación a nodos

Fuente de alcance técnico: `specs.md`. Las tareas de implementación permanecen pendientes de C1–C5. La documentación ya creada no implica aceptación funcional.

## 1. Aclaraciones pendientes

- [ ] 1.1 Resolver C1: cantidad exacta o máxima, valor previsto nulo y comportamiento ante discrepancias en alta/baja.
- [ ] 1.2 Resolver C2: traslado entre nodos y sus validaciones.
- [ ] 1.3 Resolver C3: estados y transiciones permitidas.
- [ ] 1.4 Resolver C4: baja física o lógica y conservación de lecturas y mantenimientos.
- [ ] 1.5 Resolver C5: unidad y validaciones de potencia, compatibles con los datos existentes.
- [ ] 1.6 Incorporar las respuestas aprobadas en `specs.md` antes de implementar. (C1–C5)

## 2. Domain

- [ ] 2.1 Extender `Panel` con comportamiento de actualización y validación, conservando identificador, datos y relación obligatoria con `Nodo`. (D1)
- [ ] 2.2 Implementar la regla aprobada de cantidades y el rechazo sin cambios parciales; incluir traslado solo si está autorizado. (D2)
- [ ] 2.3 Implementar estados y comportamiento de baja según C3 y C4. (D1, D2)
- [ ] 2.4 Añadir pruebas de dominio para creación, entradas inválidas, asociación, modificación, estados y cantidades. (V1)

## 3. Application

- [ ] 3.1 Definir DTOs, `IPanelService` e `IPanelRepository` con las operaciones especificadas. (A1)
- [ ] 3.2 Implementar `PanelService` con validación de existencia, comportamiento de dominio y errores controlados. (A1)
- [ ] 3.3 Comprobar identidad y roles en todas las operaciones antes de acceder a datos. (A2)

## 4. Infrastructure

- [ ] 4.1 Implementar `PanelRepository` y registrar sus dependencias reutilizando la persistencia existente. (I1)
- [ ] 4.2 Implementar escrituras atómicas y coordinación concurrente por nodo conforme a C1; coordinar ambos nodos si se aprueba traslado. (I2)
- [ ] 4.3 Ajustar mapeos y generar una migración incremental solo si las decisiones aprobadas requieren cambios de esquema, conservando datos y dependencias. (I1)

## 5. Presentation

- [ ] 5.1 Crear el listado de paneles y su consulta por nodo, mostrando la asociación. (P1)
- [ ] 5.2 Crear formularios Radzen de alta y edición con los datos aprobados. (P1)
- [ ] 5.3 Incorporar confirmación de baja y mensajes de carga, validación, conflicto y resultado. (P1)
- [ ] 5.4 Proteger rutas y acciones por rol, consumiendo solo servicios de Application. (P1, A2)

## 6. Verificación

- [ ] 6.1 Ejecutar las pruebas de Domain y las integraciones de ABM, cantidades, concurrencia y conservación de dependencias. (V1, V2)
- [ ] 6.2 Verificar consultas y escritura por rol, incluido el rechazo de identidades inválidas. (V3)
- [ ] 6.3 Comprobar en interfaz listados, formularios, asociación, confirmación de baja y errores. (V4)
- [ ] 6.4 Compilar y ejecutar `CodeQuality.Tests` y las suites afectadas; registrar resultados y omisiones. (V5)

## 7. Cierre y documentación

- [x] 7.1 Crear `resumen.md` con el estado actual del requerimiento y las decisiones pendientes.
- [ ] 7.2 Al completar la implementación, actualizar `implementations.md` con cambios, archivos y verificaciones reales, y `resumen.md` con una síntesis breve del resultado. (Sección 7 de specs)

La síntesis está completada; la implementación funcional sigue pendiente de las aclaraciones anteriores.
