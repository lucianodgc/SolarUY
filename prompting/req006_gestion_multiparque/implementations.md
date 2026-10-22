# Implementación: Gestión multiparque con configuraciones independientes

## Alcance implementado

Se implementó la edición de los datos actualmente aprobados de `ParqueSolar`: `Nombre`, `Ubicacion` y `CapacidadEstimadaMW`. El alta y el listado existentes se conservaron; al listado se le agregaron la búsqueda parcial por nombre o ubicación, el acceso a edición y la baja.

La edición preserva `Id` y `FechaCreacion`, valida las invariantes en Domain, verifica la unicidad del nombre excluyendo el mismo parque y mantiene el índice único de la base como protección ante concurrencia. La interfaz está restringida al rol `Administrador` y muestra mensajes para éxito, validación, duplicidad, inexistencia y errores inesperados.

La baja requiere confirmación explícita y se ejecuta dentro de una transacción. Elimina primero tareas de mantenimiento, lecturas cuando la tabla existe, paneles y nodos, y finalmente el parque. No se creó una migración: la implementación no cambió el esquema de la base de datos.

## Archivos modificados o creados

- `SolarUY.Domain/Entities/ParqueSolar.cs`: comportamiento `ActualizarDatos` y validaciones compartidas con el constructor.
- `SolarUY.Domain.Tests/ParqueSolarTests.cs`: pruebas de actualización e invariantes.
- `SolarUY.Application/ParquesSolares/ActualizarParqueSolarDto.cs`: DTO de edición.
- `SolarUY.Application/ParquesSolares/ActualizarParqueSolarDtoValidator.cs`: validación de edición.
- `SolarUY.Application/Exceptions/ParqueSolarNoEncontradoException.cs`: error de negocio para edición de un parque inexistente.
- `SolarUY.Application/Abstractions/IApplicationDbContext.cs`, `SolarUY.Application/ParquesSolares/IParqueSolarService.cs` y `SolarUY.Application/ParquesSolares/ParqueSolarService.cs`: contratos y caso de uso de consulta por identificador y actualización.
- `SolarUY.Infrastructure/Persistence/ApplicationDbContext.cs`: implementación de persistencia para obtener, buscar, actualizar y eliminar parques en cascada, incluida la exclusión del propio `Id` en la comprobación de nombre.
- `SolarUY.Presentation/Program.cs`: registro del validador de edición.
- `SolarUY.Presentation/Components/Pages/ParquesSolares.razor`: búsqueda, enlace de edición y baja confirmada por parque.
- `SolarUY.Presentation/Components/Pages/EditarParqueSolar.razor`: página protegida de edición.
- `SolarUY.Application.Tests/ParqueSolarServiceTests.cs`: adaptación del doble de prueba al contrato de persistencia ampliado.
- `prompting/req006_gestion_multiparque/tasks.md`: actualización del estado de las tareas.

## Resultado de pruebas

- `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj`: correcto, 16 pruebas aprobadas.
- `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj`: correcto, 9 pruebas aprobadas.
- `dotnet test SolarUY.Application.Tests/SolarUY.Application.Tests.csproj`: correcto, 8 pruebas aprobadas.
