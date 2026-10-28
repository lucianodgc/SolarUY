# Implementación: Autorización por roles para la gestión solar

## Cambios realizados

- `SolarUY.Application/ParquesSolares/ParqueSolarService.cs`: las consultas de parques requieren ahora usuario autenticado con identidad y uno de los roles `Administrador`, `Tecnico` o `Consulta`. Crear, actualizar y eliminar mantienen la comprobación de Administrador antes de validar, leer o persistir.
- `SolarUY.Presentation/Components/Pages/ParquesSolares.razor`: el listado quedó disponible para los tres roles; alta, edición y eliminación siguen siendo solo para Administrador. Se ocultaron sus enlaces y controles para los demás roles.
- `SolarUY.Presentation/Components/Layout/NavMenu.razor` y `SolarUY.Presentation/Components/Pages/Home.razor`: se habilitó el acceso de lectura a parques para los tres roles y se mantuvo el enlace de alta solo para Administrador.
- `SolarUY.Application.Tests/ParqueSolarServiceTests.cs`: se cubrieron las consultas para los tres roles, el rechazo de identidades no autorizadas y la denegación de escrituras para Técnico y Consulta antes de efectos de persistencia.
- `SolarUY.Integration.Tests/PresentationAuthorizationTests.cs`: se agregaron pruebas de metadatos `[Authorize]` para las páginas de parques, mantenimiento e inversor.

## Revisión de alcance

- Domain no requiere cambios.
- Mantenimiento ya valida sus roles y el alcance de las tareas en `TareaMantenimientoService`; sus reglas se mantienen según `req004_Mantenimiento_de_paneles`.
- El simulador del inversor ya permite consulta a los tres roles y restringe órdenes en `InversorService`. Las pruebas existentes cubren a Técnico, Consulta y usuarios sin permisos.
- No se encontraron servicios ni páginas CRUD de nodos o paneles en la solución actual. Por tanto, no hay puntos de entrada existentes donde aplicar autorización. Agregar esos casos de uso queda pendiente de un requisito de funcionalidad/implementación separado; no se inventaron contratos ni modelos en esta tarea.
- No se hicieron cambios en Infrastructure, Keycloak ni esquema de base de datos.

## Archivos modificados

- `SolarUY.Application/ParquesSolares/ParqueSolarService.cs`
- `SolarUY.Presentation/Components/Pages/ParquesSolares.razor`
- `SolarUY.Presentation/Components/Layout/NavMenu.razor`
- `SolarUY.Presentation/Components/Pages/Home.razor`
- `SolarUY.Application.Tests/ParqueSolarServiceTests.cs`
- `SolarUY.Integration.Tests/PresentationAuthorizationTests.cs`
- `prompting/req009_autorizacion_por_roles/tasks.md`
- `prompting/req009_autorizacion_por_roles/implementations.md`

## Verificaciones

Se ejecutó `dotnet test SolarUY.sln` **dos veces**:

1. Primera ejecución: pasaron 46 pruebas de Domain y 16 de Application. La compilación de Presentation falló por contextos `AuthorizeView` anidados con el mismo nombre; se corrigió asignando un contexto explícito al componente interior.
2. Segunda ejecución: pasaron 46 pruebas de Domain, 16 de Application, 25 de Integration y 9 de CodeQuality. Se omitieron 33 pruebas de integración que ya están marcadas `[Skip]`. No hubo fallos.

Los tests sirvieron para detectar el error de compilación Razor en la primera ejecución y validar tanto las reglas por rol agregadas como la compilación de las páginas y las convenciones arquitectónicas en la segunda. No se hizo una comprobación manual en navegador ni una prueba con Keycloak real.
