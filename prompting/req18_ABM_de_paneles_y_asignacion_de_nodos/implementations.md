# Implementación: ABM de paneles y asignación a nodos

## Cambios realizados

- Se confirmaron e incorporaron las decisiones de capacidad máxima, traslado entre nodos, estados y baja lógica.
- `Panel` encapsula actualización, traslado y baja lógica. Valida textos obligatorios, potencia positiva, fecha UTC y estado definido.
- `Nodo` valida la capacidad máxima configurada; `null` no impone límite.
- Se incorporaron `IPanelService`, `PanelService`, DTOs, validadores y el puerto de persistencia para crear, consultar, listar por nodo, editar, trasladar y dar de baja paneles.
- La persistencia bloquea el nodo dentro de una transacción al crear o trasladar, evitando superar la capacidad por escrituras concurrentes. El traslado conserva `PanelId`, por lo que lecturas y mantenimientos permanecen asociados al mismo panel.
- La baja lógica usa `Panel.EstaDadoDeBaja`. Los paneles dados de baja se excluyen del ABM, del inventario de inversor y de la recolección de lecturas, sin eliminar su historial.
- Se agregó la migración `AddBajaLogicaPanel`, limitada a la columna `EstaDadoDeBaja` de `Paneles`.
- Se agregaron páginas Blazor/Radzen para listado general y por nodo, alta, edición/traslado y confirmación de baja, con rutas y acciones protegidas por rol.

## Archivos principales

- Domain: `SolarUY.Domain/Entities/Panel.cs`, `SolarUY.Domain/Entities/Nodo.cs` y `SolarUY.Domain.Tests/PanelNodoTests.cs`.
- Application: carpeta `SolarUY.Application/Paneles/`.
- Infrastructure: `PanelRepository`, `PanelConfiguration`, `InversorInventarioRepository`, `RecoleccionLecturasRepository` y migración `20261117032334_AddBajaLogicaPanel`.
- Presentation: `Paneles.razor`, `AltaPanel.razor`, `EditarPanel.razor`, registros de DI y navegación.

## Verificaciones ejecutadas

- `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj --no-restore`: 71 pruebas aprobadas.
- `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj --no-restore`: 9 pruebas aprobadas.
- No se aplicó la migración sobre MariaDB ni se ejecutaron pruebas de autorización/integración contra una base configurada.
