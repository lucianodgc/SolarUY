# Implementación: Simulador del inversor

## Selector de parque (05/11/2026)

En `SimuladorInversor.razor` se agregó un selector de parques y se filtraron los nodos por el parque elegido. Cambiar de parque limpia el estado anterior, selecciona su primer nodo y actualiza la suscripción SignalR; un parque vacío muestra un mensaje. Seleccionar un nodo consulta su estado y permisos. Se conservan el parque y el nodo visibles sobre los controles. Se actualizaron requirements, specs y tasks del requerimiento.

Verificación: la pantalla compiló y las 9 pruebas de CodeQuality pasaron. Se usó una carpeta de salida separada para evitar archivos bloqueados por la aplicación en ejecución. No se ejecutó una comprobación de navegador de este selector; los avisos existentes de NuGet y Sonar continúan.

## Seguimiento H-07: pruebas permanentes

Por pedido explícito del usuario se incorpora `SolarUY.Integration.Tests` a `SolarUY.sln`, con 19 casos xUnit que resuelven el servicio de Application y el adaptador real mediante `AddSolarInfrastructure`. Se controlan reloj e identidad, sin leer configuración privada ni conectarse a MariaDB/Keycloak. Se cubren los criterios de REQ5 y las restricciones de operación por rol. No se alteran Domain ni el simulador ni se crean pruebas unitarias de Infrastructure. Las tareas 1.6 y 3.5 quedan cerradas con esta entrega ejecutada; las referencias a pendientes en las secciones históricas siguientes corresponden a las revisiones anteriores.

Ver [instrucciones de la suite](../../SolarUY.Integration.Tests/README.md) y [seguimiento de hallazgos](../seguimiento_auditoria.md).

## Ampliación de frontend solicitada

Se agrega `/simulador` con enumeración de nodos, paneles, potencia y volcado a red. `InversorService` autoriza cada consulta/orden y entrega datos calculados; Administrador opera y Técnico/Consulta solo consultan, según confirmación del usuario. `IInversorClient` enumera su topología sin acoplar la UI al adaptador ni al inventario persistido. La simulación sigue siendo en memoria por proceso.

Se hicieron comprobaciones temporales de integración y recorridos con los tres roles. No se crearon suites unitarias de Infrastructure. La revisión posterior deja explícitamente pendientes las tareas 1.6 y 3.5: un ejecutable local ignorado no equivale a entregar las pruebas permanentes pedidas por el requerimiento. Ver [frontend_integracion.md](../frontend_integracion.md). Las secciones posteriores conservan el informe original previo a esta ampliación.

## Cambios realizados

- Se definió `IInversorClient` en Application con consultas de estado por nodo y potencia total, activación/desactivación de paneles y control/consulta del volcado a red.
- Se agregó `PanelInversorEstadoDto` con identificadores de nodo y panel, estado activo, salud y potencia individual.
- Se implementó `InversorSimulador` en Infrastructure. Mantiene el estado operativo en memoria con sincronización, simula hasta 400 W aleatorios por panel sin depender de la hora real, y devuelve potencia cero si el inversor está apagado o el panel está desactivado o fallado.
- La aplicación carga nodos y paneles con sus estados y potencias nominales desde MariaDB y los sincroniza con el simulador por TCP. No se generan nodos ni paneles de ejemplo. Los paneles con falla se pueden indicar en `InversorSimulador:PanelesConFalla`.
- `AddSolarInfrastructure` registra el simulador como singleton y `TimeProvider.System` como reloj.

### Actualización periódica de la pantalla

La página `/simulador` vuelve a consultar el estado cada cinco segundos mientras está abierta. Omite el ciclo si hay una orden en curso, mantiene los datos visibles durante las órdenes y cancela el temporizador al salir de la página.

## Archivos modificados o creados

- `SolarUY.Application/Abstractions/IInversorClient.cs` (nuevo)
- `SolarUY.Infrastructure/Inversores/InversorSimulador.cs` (nuevo)
- `SolarUY.Infrastructure/DependencyInjection.cs` (modificado)
- `prompting/req5_Simulador del inversor/specs.md` (decisiones concretadas y conflicto documentado)
- `prompting/req5_Simulador del inversor/tasks.md` (tareas implementadas marcadas)
- `prompting/req5_Simulador del inversor/implementations.md` (nuevo)

## Verificación

- `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj --no-restore --disable-build-servers -m:1 -p:BuildProjectReferences=false`: pasó; 9 pruebas correctas. Se deshabilitó la reconstrucción de referencias para poder ejecutar `CodeQuality.Tests` mientras `SolarUY.Presentation/Components/App.razor` siga ausente e ignorado por Git. Por eso la compilación integral del proyecto Presentation no queda validada por esta ejecución.
- En el intento de compilación integral, MSBuild compiló `SolarUY.Infrastructure` correctamente antes de detenerse en `Program.cs`, que referencia el tipo faltante `App`.
- La regla `.agents/rules/6_flujo_trabajo_ia.md` prohíbe crear pruebas unitarias para Infrastructure. Por ello no se agregaron tests de la lógica del simulador; la tarea 3.5 queda pendiente hasta que se resuelva el conflicto con el requisito.
- No se modificó Presentation ni `CodeQuality.Tests`.
