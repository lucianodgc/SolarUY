# Especificaciones técnicas: Simulador del inversor

## Contexto y alcance

La funcionalidad introduce un simulador de inversor sustituible por una integración real a través de `IInversorClient`. La abstracción pertenece a `Application` y la implementación simulada a `Infrastructure`, según Clean Architecture. `Domain` no necesita cambios mientras la funcionalidad no agregue reglas o entidades de negocio existentes fuera de este puerto de integración.

Los requisitos no fijan la forma de identificar nodos y paneles, los tipos de retorno de las operaciones, el formato de los estados, el rango de potencia ni el mecanismo y valores para configurar las fallas. Esas decisiones deben aclararse antes de cerrar los contratos y el comportamiento. No se presuponen entidades `Nodo` o `Panel` ni dependencias tecnológicas nuevas.

## Decisiones aplicadas

1. El simulador recibe desde la aplicación una copia actual del inventario de nodos y paneles persistidos. Los identificadores Guid se transmiten como texto para conservar el contrato actual. No crea ni modifica registros del inventario.
2. Las operaciones son asíncronas y aceptan `CancellationToken`. Las consultas devuelven estado por panel y potencia total del nodo; identificadores desconocidos producen `KeyNotFoundException`.
3. La potencia se expresa en watts, con máximo de 400 W por panel y variación aleatoria independiente del reloj local. Un panel activo, sano y con el inversor encendido puede generar a cualquier hora.
4. Las fallas se configuran determinísticamente mediante una lista de identificadores de panel. Un panel fallado conserva su estado de salud `Fallado` y genera cero watts. El estado operativo de activación es independiente.
5. El simulador se registra como singleton para compartir estado en el proceso; no conserva estado tras reinicios ni coordina varias instancias.
6. Además de la evidencia temporal, el pedido explícito de seguimiento del usuario se concreta con una suite permanente de integración xUnit en `SolarUY.Integration.Tests`, incluida en la solución. Usa el registro real de Infrastructure, los casos de uso de Application y reloj/identidad controlados, sin nuevas pruebas unitarias de Infrastructure ni dependencias de MariaDB/Keycloak.
7. La pantalla usa los nodos, parques, paneles, números de serie, estados y potencias nominales del inventario persistido. Administrador puede operar el estado simulado; Técnico y Consulta solo leen. Las órdenes no persisten cambios en las entidades.

## Cambios por capa

### 1. Domain

No requiere modificaciones para esta tarea. No se agregan entidades de panel/inversor ni reglas de dominio que no estén definidas en el modelo existente. La potencia sintética y el estado de simulación son responsabilidad del adaptador de Infrastructure, expuesto mediante el contrato de Application.

### 2. Application

Definir `IInversorClient` en `SolarUY.Application` como puerto de la integración. Expone consultas asíncronas de estado por panel y potencia total por nodo, operaciones para activar/desactivar un panel y el volcado a red, y una consulta del estado del volcado. Usa identificadores de texto, DTOs de Application y `CancellationToken` opcional. Los identificadores desconocidos generan `KeyNotFoundException`.

La interfaz no depende de `Infrastructure`, Blazor, EF Core ni del proveedor de simulación. Para la ampliación web se agrega `ListarNodosAsync` al puerto y se incorporan `IInversorService`/`InversorService` con inyección por constructor. El servicio autoriza mediante `IUsuarioActual` cada consulta y orden. `EstadoInversorDto` entrega paneles, potencia total calculada desde la misma lectura de paneles, estado del volcado, instante UTC y `PuedeOperar`. La UI no calcula potencia ni decide permisos de operación.

### 3. Infrastructure

Implementar `IInversorClient` en Infrastructure con un simulador cuyo estado sea coherente entre consultas y órdenes durante su ciclo de vida configurado. Debe mantener el estado de activación de cada panel y del volcado a red; desactivar un panel impide que reporte generación y reactivarlo lo incorpora nuevamente. Las órdenes de un panel afectan solo a ese panel. Las consultas de estado deben incluir la salud simulada y reflejar las acciones persistidas en memoria.

Calcular potencia aleatoria por panel con máximo de 400 W, sin consultar la hora real. Un inversor apagado, panel desactivado o panel fallado no genera potencia. Los identificadores de panel fallado se reciben mediante `InversorSimulador:PanelesConFalla`; el estado devuelve su salud como `Fallado`.

Registrar el adaptador en el punto existente de composición de dependencias de Infrastructure para que el host pueda resolver `IInversorClient`. La implementación no debe ser referenciada directamente por Domain o Application. No se introduce una API, dispositivo real, persistencia, biblioteca o servicio externo.

El simulador es singleton dentro del proceso para que consultas y órdenes compartan estado. No persiste entre reinicios ni coordina el estado entre instancias.

La enumeración de nodos devuelve la última topología sincronizada desde el inventario. No hay datos predeterminados si la base no contiene nodos. Se verifican los comportamientos con pruebas permanentes de integración, sin incorporar pruebas unitarias de Infrastructure ni modificar guardarraíles.

### 4. Presentation

`SimuladorInversor.razor`, en `/simulador`, utiliza exclusivamente `IInversorService`. Presenta selección de nodo del inventario, parque, paneles identificados por número de serie, potencia en watts, estado operativo/salud y estado del volcado. Actualiza las lecturas automáticamente cada cinco segundos y permite actualización manual. Las acciones se muestran según `PuedeOperar`; el servicio valida los permisos nuevamente. Técnico y Consulta solo leen. Los estados son simulados y los controles no modifican MariaDB. Se deshabilitan acciones durante una operación, se conserva la vista mientras se procesa y se muestran errores controlados, estados vacíos y la hora UTC de consulta. El temporizador se cancela al salir de la página.

## Verificación prevista

Ejecutar `dotnet test SolarUY.sln` para incluir los casos permanentes del simulador y las pruebas existentes. Los casos verifican inventario sincronizado, lecturas aleatorias limitadas por potencia nominal, independencia de la hora, estado/salud, operaciones, aislamiento, composición por ámbitos y permisos. No sustituyen los recorridos OIDC ni una prueba con MariaDB, documentados en la auditoría integral y su seguimiento. Los guardarraíles se ejecutan sin modificar sus pruebas.
