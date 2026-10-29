# Implementación: gestión de nodos e inversores

## Resultado

Se implementó la gestión de nodos con su inversor persistido, conforme a las decisiones C1--C4: el nodo conserva solo `Nombre`, `UbicacionEnParque` y `CantidadPanelesPrevista`; el inversor se crea con el nodo; sus estados son `Encendido` y `Apagado`; y al eliminar el nodo se eliminan sus dependencias.

## Cambios realizados

- Domain: se agregaron `Inversor` y `EstadoInversor`; `Nodo` permite actualizar sus tres datos y mantiene la relación con su inversor; `ParqueSolar` controla sus nodos y rechaza eliminar el último.
- Application: se agregaron DTOs, validadores, `INodoService`, `NodoService` e `INodoRepository`. La autorización se aplica dentro de los casos de uso: Administrador escribe; Administrador, Tecnico y Consulta leen.
- Infrastructure: se configuró la relación EF uno-a-uno `Nodo`--`Inversor`, se agregó `NodoRepository`, el conjunto `Inversores` y la migración `20261028235200_AddInversorNodo`. La baja se ejecuta en transacción y elimina tareas, lecturas, paneles, inversor y nodo, en ese orden.
- Presentation: se incorporaron las páginas `NodosParque`, `AltaNodo` y `EditarNodo`, protegidas por rol y construidas con Radzen. El listado de parques enlaza a los nodos para todos los roles de consulta.
- Pruebas: se agregaron pruebas de dominio para nodo, parque e inversor, pruebas de integración de los casos de uso y se extendió la comprobación de atributos de autorización de las páginas.

## Verificaciones ejecutadas

| Verificación | Resultado |
| --- | --- |
| `dotnet build SolarUY.sln --no-restore` | Correcta, sin advertencias ni errores. |
| `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj --no-restore` | 51 aprobadas. |
| Pruebas de integración de nodos | 5 aprobadas. |
| `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj --no-restore` | 9 aprobadas. |
| Suite de integración existente | 28 aprobadas y 33 omitidas por no estar configurada la base aislada requerida por esos casos. |
| `dotnet ef migrations has-pending-model-changes` | Sin cambios pendientes respecto de la migración. |
| Script EF de `AddInversorNodo` | Verificado: crea `Inversores`, FK a `Nodos` e índice único de `NodoId`. |

## Pendiente operativo

No se aplicó `database update` sobre MariaDB porque la conexión disponible para el entorno devolvió acceso denegado para el usuario configurado. Tampoco se ejecutó una comprobación visual en navegador. La tarea 4.3 y la 5.5 permanecen pendientes hasta disponer de una base MariaDB autorizada y una sesión de navegador de prueba.
