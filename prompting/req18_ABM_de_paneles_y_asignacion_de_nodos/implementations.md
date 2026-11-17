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

## Persistencia y permisos verificados — 17/11/2026

Se reprodujo y corrigió un error real al crear o trasladar paneles: adjuntar el grafo del nodo duplicaba una clave ya seguida por EF al bloquear capacidad. La persistencia ahora registra únicamente la entidad Panel, conservando bloqueos y transacciones.

La migración se aplicó desde cero en MariaDB aislada. Cuatro integraciones comprobaron creación, traslado con procedencia histórica intacta, mantenimiento conservado, baja lógica excluida del inventario, dos altas simultáneas para el último cupo y rechazo de escrituras por Tecnico/Consulta con lectura permitida. Las cuatro aprobaron; también los tres escenarios de borrado protegido. Arquitectura: 9 aprobadas, guardarraíles sin cambios. Evidencia: `artifacts/correcciones/paneles-final/` y `artifacts/correcciones/arquitectura-final/`.

La revisión visual con sesiones OIDC reales sigue pendiente en 6.4; estas comprobaciones no la sustituyen.
