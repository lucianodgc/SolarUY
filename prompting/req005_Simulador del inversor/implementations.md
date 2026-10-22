# Implementación: Simulador del inversor

## Seguimiento H-07: pruebas permanentes

Por pedido explícito del usuario se incorpora `SolarUY.Integration.Tests` a `SolarUY.sln`, con 19 casos xUnit que resuelven el servicio de Application y el adaptador real mediante `AddSolarInfrastructure`. Se controlan reloj e identidad, sin leer configuración privada ni conectarse a MariaDB/Keycloak. Se cubren los criterios de REQ005 y las restricciones de operación por rol. No se alteran Domain ni el simulador ni se crean pruebas unitarias de Infrastructure. Las tareas 1.6 y 3.5 quedan cerradas con esta entrega ejecutada; las referencias a pendientes en las secciones históricas siguientes corresponden a las revisiones anteriores.

Ver [instrucciones de la suite](../../SolarUY.Integration.Tests/README.md) y [seguimiento de hallazgos](../seguimiento_auditoria.md).

## Ampliación de frontend solicitada

Se agrega `/simulador` con enumeración de nodos, paneles, potencia y volcado a red. `InversorService` autoriza cada consulta/orden y entrega datos calculados; Administrador opera y Técnico/Consulta solo consultan, según confirmación del usuario. `IInversorClient` enumera su topología sin acoplar la UI al adaptador ni al inventario persistido. La simulación sigue siendo en memoria por proceso.

Se hicieron comprobaciones temporales de integración y recorridos con los tres roles. No se crearon suites unitarias de Infrastructure. La revisión posterior deja explícitamente pendientes las tareas 1.6 y 3.5: un ejecutable local ignorado no equivale a entregar las pruebas permanentes pedidas por el requerimiento. Ver [frontend_integracion.md](../frontend_integracion.md). Las secciones posteriores conservan el informe original previo a esta ampliación.

## Cambios realizados

- Se definió `IInversorClient` en Application con consultas de estado por nodo y potencia total, activación/desactivación de paneles y control/consulta del volcado a red.
- Se agregó `PanelInversorEstadoDto` con identificadores de nodo y panel, estado activo, salud y potencia individual.
- Se implementó `InversorSimulador` en Infrastructure. Mantiene el estado operativo en memoria con sincronización, simula 400 W máximos por panel con una curva sinusoidal entre 06:00 y 18:00 locales, y devuelve potencia cero para paneles desactivados o fallados.
- La topología se configura en `InversorSimulador:Nodos` con valores separados por coma. Si no se configura, se usan `nodo-1` y `panel-1` a `panel-4`. Los paneles con falla se listan en `InversorSimulador:PanelesConFalla`.
- `AddSolarInfrastructure` registra el simulador como singleton y `TimeProvider.System` como reloj.

## Archivos modificados o creados

- `SolarUY.Application/Abstractions/IInversorClient.cs` (nuevo)
- `SolarUY.Infrastructure/Inversores/InversorSimulador.cs` (nuevo)
- `SolarUY.Infrastructure/DependencyInjection.cs` (modificado)
- `prompting/req005_Simulador del inversor/specs.md` (decisiones concretadas y conflicto documentado)
- `prompting/req005_Simulador del inversor/tasks.md` (tareas implementadas marcadas)
- `prompting/req005_Simulador del inversor/implementations.md` (nuevo)

## Verificación

- `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj --no-restore --disable-build-servers -m:1 -p:BuildProjectReferences=false`: pasó; 9 pruebas correctas. Se deshabilitó la reconstrucción de referencias para poder ejecutar `CodeQuality.Tests` mientras `SolarUY.Presentation/Components/App.razor` siga ausente e ignorado por Git. Por eso la compilación integral del proyecto Presentation no queda validada por esta ejecución.
- En el intento de compilación integral, MSBuild compiló `SolarUY.Infrastructure` correctamente antes de detenerse en `Program.cs`, que referencia el tipo faltante `App`.
- La regla `.agents/rules/6_flujo_trabajo_ia.md` prohíbe crear pruebas unitarias para Infrastructure. Por ello no se agregaron tests de la lógica del simulador; la tarea 3.5 queda pendiente hasta que se resuelva el conflicto con el requisito.
- No se modificó Presentation ni `CodeQuality.Tests`.
