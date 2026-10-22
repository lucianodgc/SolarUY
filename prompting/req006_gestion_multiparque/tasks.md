# Tareas de implementación: Gestión multiparque con configuraciones independientes

## 1. Aclaraciones de alcance

- [x] 1.1 Definir búsqueda parcial por nombre y ubicación; un criterio vacío muestra todos los parques.
- [x] 1.2 Confirmar que no existen parámetros de configuración adicionales a `Nombre`, `Ubicacion` y `CapacidadEstimadaMW`.
- [x] 1.3 Definir la baja física en cascada de las dependencias, previa confirmación explícita del Administrador.

## 2. Capa Domain

- [x] 2.1 Incorporar en `ParqueSolar` el comportamiento de actualización de nombre, ubicación y capacidad estimada, preservando `Id` y `FechaCreacion`.
- [x] 2.2 Aplicar al comportamiento de actualización las invariantes de obligatoriedad y longitudes ya definidas para nombre y ubicación.
- [x] 2.3 Crear pruebas unitarias de Domain para actualización válida, preservación de identidad y fecha, y entradas inválidas.

## 3. Capa Application

- [x] 3.1 Crear el DTO de edición sin campos de configuración adicionales.
- [x] 3.2 Crear el validador del DTO de edición.
- [x] 3.3 Extender `IParqueSolarService` con obtención por `Id`, búsqueda parcial por nombre o ubicación, actualización y baja en cascada.
- [x] 3.4 Extender la abstracción de persistencia para obtener por `Id`, actualizar y comprobar un nombre duplicado excluyendo el propio parque.
- [x] 3.5 Implementar los casos de uso de consulta por `Id` y edición: validar, informar inexistencia, preservar unicidad y delegar la mutación a `ParqueSolar`.
- [x] 3.6 Implementar el caso de uso de baja en cascada e informar la inexistencia del parque mediante un error de negocio distinguible.

## 4. Capa Infrastructure

- [x] 4.1 Implementar en `ApplicationDbContext` la consulta por `Id`, búsqueda parcial por nombre o ubicación y actualización requeridas por la abstracción de Application.
- [x] 4.2 Mantener la restricción única de `Nombre` y traducir sus conflictos de creación o edición al error de negocio correspondiente.
- [x] 4.3 Implementar la baja transaccional en cascada de dependencias existentes en persistencia.
- [x] 4.4 Confirmar que la baja transaccional no cambia el esquema, por lo que no requiere migración ni modificación del snapshot.

## 5. Capa Presentation

- [x] 5.1 Reutilizar `AltaParqueSolar.razor` para el alta y conservar su restricción al rol Administrador.
- [x] 5.2 Ampliar `ParquesSolares.razor` con búsqueda parcial por nombre o ubicación. El listado, sus estados y la navegación a edición están disponibles.
- [x] 5.3 Crear la página de edición protegida para Administrador, con carga por `Id`, formulario de datos existentes y mensajes de éxito, validación, duplicidad, inexistencia y error inesperado.
- [x] 5.4 Incorporar la acción de baja protegida para Administrador, con confirmación y mensajes acordes a la eliminación en cascada.
- [x] 5.5 Integrar menú/inicio, acceso al alta desde el listado, controles de carga y envíos repetidos, alojamiento del diálogo Radzen y corrección del formulario de edición para conservarlo visible ante errores de validación.

## 6. Verificación

- [x] 6.1 Ejecutar las pruebas unitarias de Domain, incluidas las pruebas de actualización de `ParqueSolar`.
- [x] 6.2 Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` y corregir cualquier incumplimiento de arquitectura o nomenclatura sin modificar los tests estructurales.
- [x] 6.3 Validar en navegador como Administrador alta, listado, búsqueda, edición, baja de un parque temporal y cancelación del diálogo; comprobar con Técnico y Consulta la denegación del acceso directo a gestión de parques. Evidencia en `prompting/frontend_integracion.md`.

## 7. Cierre y documentación

- [x] 7. Crear el archivo implementations.md en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
