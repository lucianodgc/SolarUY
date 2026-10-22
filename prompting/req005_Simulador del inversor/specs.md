# Especificaciones técnicas: Simulador del inversor

## Contexto y alcance

La funcionalidad introduce un simulador de inversor sustituible por una integración real a través de `IInversorClient`. La abstracción pertenece a `Application` y la implementación simulada a `Infrastructure`, según Clean Architecture. `Domain` no necesita cambios mientras la funcionalidad no agregue reglas o entidades de negocio existentes fuera de este puerto de integración.

Los requisitos no fijan la forma de identificar nodos y paneles, los tipos de retorno de las operaciones, el formato de los estados, el rango de potencia ni el mecanismo y valores para configurar las fallas. Esas decisiones deben aclararse antes de cerrar los contratos y el comportamiento. No se presuponen entidades `Nodo` o `Panel` ni dependencias tecnológicas nuevas.

## Decisiones aplicadas

1. El puerto conserva los identificadores `string` y DTO de Application definidos originalmente antes de la persistencia de nodos/paneles de req004. La simulación utiliza su propia topología: configurable mediante mapa nodo-panel o, por defecto, `nodo-1` con `panel-1` a `panel-4`. No se vincula automáticamente a los equipos persistidos.
2. Las operaciones son asíncronas y aceptan `CancellationToken`. Las consultas devuelven estado por panel y potencia total del nodo; identificadores desconocidos producen `KeyNotFoundException`.
3. La potencia se expresa en watts, con máximo de 400 W por panel y curva sinusoidal entre las 06:00 y las 18:00 según la hora local del `TimeProvider`. Fuera de ese intervalo la potencia es cero.
4. Las fallas se configuran determinísticamente mediante una lista de identificadores de panel. Un panel fallado conserva su estado de salud `Fallado` y genera cero watts. El estado operativo de activación es independiente.
5. El simulador se registra como singleton para compartir estado en el proceso; no conserva estado tras reinicios ni coordina varias instancias.
6. Además de la evidencia temporal, el pedido explícito de seguimiento del usuario se concreta con una suite permanente de integración xUnit en `SolarUY.Integration.Tests`, incluida en la solución. Usa el registro real de Infrastructure, los casos de uso de Application y reloj/identidad controlados, sin nuevas pruebas unitarias de Infrastructure ni dependencias de MariaDB/Keycloak.
7. La ampliación de frontend agrega consulta para Administrador, Tecnico y Consulta, y controles exclusivos de Administrador, según la respuesta explícita del usuario. Los identificadores simulados siguen siendo independientes de las entidades persistidas por req004.

## Cambios por capa

### 1. Domain

No requiere modificaciones para esta tarea. No se agregan entidades de panel/inversor ni reglas de dominio que no estén definidas en el modelo existente. La potencia sintética y el estado de simulación son responsabilidad del adaptador de Infrastructure, expuesto mediante el contrato de Application.

### 2. Application

Definir `IInversorClient` en `SolarUY.Application` como puerto de la integración. Expone consultas asíncronas de estado por panel y potencia total por nodo, operaciones para activar/desactivar un panel y el volcado a red, y una consulta del estado del volcado. Usa identificadores de texto, DTOs de Application y `CancellationToken` opcional. Los identificadores desconocidos generan `KeyNotFoundException`.

La interfaz no depende de `Infrastructure`, Blazor, EF Core ni del proveedor de simulación. Para la ampliación web se agrega `ListarNodosAsync` al puerto y se incorporan `IInversorService`/`InversorService` con inyección por constructor. El servicio autoriza mediante `IUsuarioActual` cada consulta y orden. `EstadoInversorDto` entrega paneles, potencia total calculada desde la misma lectura de paneles, estado del volcado, instante UTC y `PuedeOperar`. La UI no calcula potencia ni decide permisos de operación.

### 3. Infrastructure

Implementar `IInversorClient` en Infrastructure con un simulador cuyo estado sea coherente entre consultas y órdenes durante su ciclo de vida configurado. Debe mantener el estado de activación de cada panel y del volcado a red; desactivar un panel impide que reporte generación y reactivarlo lo incorpora nuevamente. Las órdenes de un panel afectan solo a ese panel. Las consultas de estado deben incluir la salud simulada y reflejar las acciones persistidas en memoria.

Calcular potencia por panel con una curva sinusoidal entre las 06:00 y las 18:00 locales, con máximo de 400 W y cero fuera de ese intervalo. Un panel desactivado o fallado no genera potencia. Los identificadores de panel fallado se reciben mediante `InversorSimulador:PanelesConFalla`; el estado devuelve su salud como `Fallado`.

Registrar el adaptador en el punto existente de composición de dependencias de Infrastructure para que el host pueda resolver `IInversorClient`. La implementación no debe ser referenciada directamente por Domain o Application. No se introduce una API, dispositivo real, persistencia, biblioteca o servicio externo.

El simulador es singleton dentro del proceso para que consultas y órdenes compartan estado. No persiste entre reinicios ni coordina el estado entre instancias.

La enumeración de nodos devuelve la topología configurada, ordenada por identificador, conservando el nodo predeterminado cuando no existe configuración. Se verifican los comportamientos con pruebas permanentes de integración, sin incorporar pruebas unitarias de Infrastructure ni modificar guardarraíles.

### 4. Presentation

`SimuladorInversor.razor`, en `/simulador`, utiliza exclusivamente `IInversorService`. Presenta selección de nodo, actualización manual, potencia en watts, paneles con estado operativo/salud/potencia y estado del volcado. Las acciones se muestran según `PuedeOperar`; el servicio valida los permisos nuevamente. Técnico y Consulta solo leen. Se deshabilitan acciones durante una operación y se muestran errores controlados, estados vacíos y la hora UTC de consulta. Se advierte que la simulación es independiente del inventario y que los cambios duran hasta reiniciar la aplicación. El menú y el inicio incluyen el acceso para los tres roles.

## Verificación prevista

Ejecutar `dotnet test SolarUY.sln` para incluir los 19 casos permanentes del simulador y las pruebas existentes. Los casos verifican consulta por nodo, curva diurna/nocturna, estado/salud, operaciones, aislamiento, composición por ámbitos y permisos. No sustituyen los recorridos OIDC ni una prueba con MariaDB, documentados en la auditoría integral y su seguimiento. Los guardarraíles se ejecutan sin modificar sus pruebas.
