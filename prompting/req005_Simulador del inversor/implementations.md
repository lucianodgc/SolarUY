# Implementación: Simulador del inversor

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

- `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj`: no completó porque Presentation no compila: `Program.cs` referencia el tipo `App`, pero falta `SolarUY.Presentation/Components/App.razor`. Las pruebas arquitectónicas no llegaron a ejecutarse.
- En ese intento, MSBuild compiló `SolarUY.Infrastructure` correctamente antes de detenerse al compilar Presentation.
- La regla `.agents/rules/6_flujo_trabajo_ia.md` prohíbe crear pruebas unitarias para Infrastructure. Por ello no se agregaron tests de la lógica del simulador; la tarea 3.5 queda pendiente hasta que se resuelva el conflicto con el requisito.
- No se modificó Presentation ni `CodeQuality.Tests`.
