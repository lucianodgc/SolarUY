# Tareas de implementación: gestión de nodos e inversores

Fuente técnica exclusiva: `specs.md`. Las tareas quedan pendientes y no se deben iniciar las que dependan de una aclaración sin que el equipo la confirme.

## 1. Decisiones confirmadas

- [x] 1.1 Confirmar que el nodo usa exclusivamente los atributos actuales del MER. (C1)
- [x] 1.2 Confirmar que el inversor se crea con el nodo. (C2)
- [x] 1.3 Confirmar los estados `Encendido` y `Apagado` del inversor. (C3)
- [x] 1.4 Confirmar la eliminación del inversor, paneles, tareas y lecturas junto al nodo. (C4)

## 2. Domain y pruebas

- [x] 2.1 Completar `Nodo` con validación de creación y actualización de los atributos aprobados, manteniendo la asociación obligatoria a `ParqueSolar`. (D1)
- [x] 2.2 Incorporar en `ParqueSolar` la operación que rechaza quitar el último nodo y permite quitarlo cuando permanece al menos uno. (D1)
- [x] 2.3 Crear `Inversor` con su asociación uno-a-uno a `Nodo`, atributos del MER y las invariantes aprobadas en C2 y C3. (D2)
- [x] 2.4 Implementar el comportamiento de eliminación de nodo e inversor/dependencias que se apruebe en C4. (D1, D2)
- [x] 2.5 Crear o ampliar pruebas xUnit de dominio para nodo, regla del último nodo, asociación nodo-inversor, estados y eliminación; comprobar ausencia de cambios parciales en rechazos. (D3)

## 3. Application

- [x] 3.1 Definir DTOs, interfaces de repositorio y contratos de servicio para crear, consultar por parque, editar y eliminar nodos, sin filtrar detalles de Infrastructure. (A1)
- [x] 3.2 Implementar los casos de uso de nodos, delegando las invariantes al dominio y cargando el agregado necesario para la eliminación. (A1)
- [x] 3.3 Incluir los datos del inversor asociado en las consultas y completar el flujo de alta definido en C2. (A1)
- [x] 3.4 Aplicar en cada operación la matriz de autorización de req009 mediante `IUsuarioActual`: escritura para Administrador y consulta para Administrador, Tecnico y Consulta. (A2)
- [x] 3.5 Ampliar las pruebas de integración existentes de autorización para permisos por rol, consultas y ausencia de persistencia ante denegación, sin crear pruebas unitarias fuera de Domain. (A3)

## 4. Infrastructure

- [x] 4.1 Configurar persistencia EF de `Nodo` e `Inversor`, con FK parque-nodo y FK única nodo-inversor, y extender el contexto y repositorios necesarios. (Infrastructure)
- [x] 4.2 Aplicar las restricciones de parámetros, estado y borrado aprobadas en C1, C3 y C4. (Infrastructure)
- [ ] 4.3 Generar la migración EF incremental correspondiente y comprobar su aplicación en MariaDB configurada externamente. (Infrastructure, Verificación)
- [x] 4.4 Mantener aislado el simulador de `req005`, sin vincularlo automáticamente al inventario persistido de nodos e inversores. (Infrastructure)

## 5. Presentation

- [x] 5.1 Crear la pantalla Radzen de consulta de nodos por parque con los datos del inversor asociados. (Presentation)
- [x] 5.2 Crear los formularios Radzen de alta y edición con los campos finalmente aprobados, validaciones y errores controlados. (Presentation)
- [x] 5.3 Implementar la confirmación de eliminación y la comunicación de los rechazos de negocio sin informar éxito. (Presentation)
- [x] 5.4 Proteger rutas, acciones y navegación: escritura solo para Administrador y consulta para Administrador, Tecnico y Consulta. (Presentation)
- [ ] 5.5 Verificar en interfaz los flujos administrativos, la consulta autorizada por los tres roles y los errores de validación/denegación cuando exista entorno disponible. (Verificación)

## 6. Verificación y cierre

- [x] 6.1 Ejecutar las pruebas de dominio y de integración de autorización contempladas en `specs.md`, además de `dotnet test` sobre `CodeQuality.Tests`; registrar resultados y verificaciones no ejecutadas. (Verificación)
- [x] 6.2 Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
