# Especificaciones técnicas: Simulador del inversor

## Contexto y alcance

La funcionalidad introduce un simulador de inversor sustituible por una integración real a través de `IInversorClient`. La abstracción pertenece a `Application` y la implementación simulada a `Infrastructure`, según Clean Architecture. `Domain` no necesita cambios mientras la funcionalidad no agregue reglas o entidades de negocio existentes fuera de este puerto de integración.

Los requisitos no fijan la forma de identificar nodos y paneles, los tipos de retorno de las operaciones, el formato de los estados, el rango de potencia ni el mecanismo y valores para configurar las fallas. Esas decisiones deben aclararse antes de cerrar los contratos y el comportamiento. No se presuponen entidades `Nodo` o `Panel` ni dependencias tecnológicas nuevas.

## Decisiones aplicadas y aclaración pendiente

1. No existe un modelo de nodo/panel en el código actual. El puerto usa identificadores `string` y DTOs de consulta propios de Application. La configuración permite declarar el mapa nodo-panel; si no se proporciona, se simula `nodo-1` con `panel-1` a `panel-4`.
2. Las operaciones son asíncronas y aceptan `CancellationToken`. Las consultas devuelven estado por panel y potencia total del nodo; identificadores desconocidos producen `KeyNotFoundException`.
3. La potencia se expresa en watts, con máximo de 400 W por panel y curva sinusoidal entre las 06:00 y las 18:00 según la hora local del `TimeProvider`. Fuera de ese intervalo la potencia es cero.
4. Las fallas se configuran determinísticamente mediante una lista de identificadores de panel. Un panel fallado conserva su estado de salud `Fallado` y genera cero watts. El estado operativo de activación es independiente.
5. El simulador se registra como singleton para compartir estado en el proceso; no conserva estado tras reinicios ni coordina varias instancias.
6. Sigue pendiente el conflicto de verificación: los requisitos solicitan tests de la lógica del simulador, mientras que `.agents/rules/6_flujo_trabajo_ia.md` prohíbe generar pruebas unitarias para Infrastructure. No se agregaron tests de Infrastructure ni se alteraron pruebas arquitectónicas.

## Cambios por capa

### 1. Domain

No requiere modificaciones para esta tarea. No se agregan entidades de panel/inversor ni reglas de dominio que no estén definidas en el modelo existente. La potencia sintética y el estado de simulación son responsabilidad del adaptador de Infrastructure, expuesto mediante el contrato de Application.

### 2. Application

Definir `IInversorClient` en `SolarUY.Application` como puerto de la integración. Expone consultas asíncronas de estado por panel y potencia total por nodo, operaciones para activar/desactivar un panel y el volcado a red, y una consulta del estado del volcado. Usa identificadores de texto, DTOs de Application y `CancellationToken` opcional. Los identificadores desconocidos generan `KeyNotFoundException`.

La interfaz no debe depender de `Infrastructure`, Blazor, EF Core ni del proveedor de simulación. No se agregan servicios de aplicación ni DTOs separados salvo que la definición confirmada de las respuestas los requiera.

### 3. Infrastructure

Implementar `IInversorClient` en Infrastructure con un simulador cuyo estado sea coherente entre consultas y órdenes durante su ciclo de vida configurado. Debe mantener el estado de activación de cada panel y del volcado a red; desactivar un panel impide que reporte generación y reactivarlo lo incorpora nuevamente. Las órdenes de un panel afectan solo a ese panel. Las consultas de estado deben incluir la salud simulada y reflejar las acciones persistidas en memoria.

Calcular potencia por panel con una curva sinusoidal entre las 06:00 y las 18:00 locales, con máximo de 400 W y cero fuera de ese intervalo. Un panel desactivado o fallado no genera potencia. Los identificadores de panel fallado se reciben mediante `InversorSimulador:PanelesConFalla`; el estado devuelve su salud como `Fallado`.

Registrar el adaptador en el punto existente de composición de dependencias de Infrastructure para que el host pueda resolver `IInversorClient`. La implementación no debe ser referenciada directamente por Domain o Application. No se introduce una API, dispositivo real, persistencia, biblioteca o servicio externo.

El simulador es singleton dentro del proceso para que consultas y órdenes compartan estado. No persiste entre reinicios ni coordina el estado entre instancias.

Los tests unitarios de Infrastructure quedan pendientes por el conflicto documentado con la regla del proyecto. No se modificarán pruebas arquitectónicas.

### 4. Presentation

No requiere componentes ni lógica de presentación: no se solicita una interfaz de usuario para operar el simulador. El host solo utilizará el registro de dependencias existente para componer la implementación de Infrastructure. La UI no accederá directamente a la clase simulada.

## Verificación prevista

Una vez resueltas las aclaraciones y el conflicto de pruebas, verificar los comportamientos solicitados: consulta por nodo; variación de potencia según hora; activación y desactivación; cero generación mientras el panel esté desactivado; cambio y consulta del estado de volcado; y respuesta ante fallas configuradas. Además, comprobar que la composición resuelve `IInversorClient` y ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` sin modificar sus pruebas.
