# Especificaciones técnicas: Simulador del inversor

## Contexto y alcance

La funcionalidad introduce un simulador de inversor sustituible por una integración real a través de `IInversorClient`. La abstracción pertenece a `Application` y la implementación simulada a `Infrastructure`, según Clean Architecture. `Domain` no necesita cambios mientras la funcionalidad no agregue reglas o entidades de negocio existentes fuera de este puerto de integración.

Los requisitos no fijan la forma de identificar nodos y paneles, los tipos de retorno de las operaciones, el formato de los estados, el rango de potencia ni el mecanismo y valores para configurar las fallas. Esas decisiones deben aclararse antes de cerrar los contratos y el comportamiento. No se presuponen entidades `Nodo` o `Panel` ni dependencias tecnológicas nuevas.

## Aclaraciones requeridas

1. Confirmar qué identificadores/tipos existentes representan nodo y panel y qué datos deben devolver las consultas de estado y potencia. La solución debe reutilizar el modelo aprobado; no se infieren entidades ni DTOs sin esa definición.
2. Acordar los tipos de retorno y semántica de errores de `IInversorClient` (por ejemplo, nodo o panel inexistente) y si las operaciones serán asíncronas. Los requisitos enumeran operaciones, pero no especifican sus firmas.
3. Definir el rango válido de potencia, las horas de luz solar y los límites horarios de la curva diaria. La regla disponible solo exige una curva aproximada con mínimo nocturno y máximo central.
4. Definir cómo se configura la generación de fallas (parámetro/configuración y alcance por panel o ejecución) y si una falla limita generación, cambia el estado de salud o ambas cosas. Las fallas deben poder controlarse de forma determinista para las verificaciones requeridas.
5. Resolver el conflicto de verificación: los requisitos solicitan tests de la lógica del simulador, mientras que `.agents/rules/6_flujo_trabajo_ia.md` prohíbe generar pruebas unitarias para Infrastructure. El equipo debe indicar una forma de satisfacer ambos guardarraíles, sin alterar `CodeQuality.Tests`.

## Cambios por capa

### 1. Domain

No requiere modificaciones para esta tarea. No se agregan entidades de panel/inversor ni reglas de dominio que no estén definidas en el modelo existente. La potencia sintética y el estado de simulación son responsabilidad del adaptador de Infrastructure, expuesto mediante el contrato de Application.

### 2. Application

Definir `IInversorClient` en `SolarUY.Application` como puerto de la integración. Debe exponer operaciones para consultar el estado de paneles de un nodo, consultar su potencia, activar y desactivar un panel, y activar y desactivar el volcado de energía a la red. Sus firmas, tipos de identificadores/retorno y semántica de errores quedan sujetos a las aclaraciones anteriores.

La interfaz no debe depender de `Infrastructure`, Blazor, EF Core ni del proveedor de simulación. No se agregan servicios de aplicación ni DTOs separados salvo que la definición confirmada de las respuestas los requiera.

### 3. Infrastructure

Implementar `IInversorClient` en Infrastructure con un simulador cuyo estado sea coherente entre consultas y órdenes durante su ciclo de vida configurado. Debe mantener el estado de activación de cada panel y del volcado a red; desactivar un panel impide que reporte generación y reactivarlo lo incorpora nuevamente. Las órdenes de un panel afectan solo a ese panel. Las consultas de estado deben incluir la salud simulada y reflejar las acciones persistidas en memoria.

Calcular potencia con una curva diaria aproximada dependiente de la hora: nula o mínima en horas sin luz y superior hacia las horas centrales. La salida se mantiene dentro del rango válido que se acuerde. Un panel desactivado no genera potencia. Los fallos se generan según la configuración determinista acordada y deben ser visibles de manera coherente en el estado y en la potencia conforme a la decisión pendiente.

Registrar el adaptador en el punto existente de composición de dependencias de Infrastructure para que el host pueda resolver `IInversorClient`. La implementación no debe ser referenciada directamente por Domain o Application. No se introduce una API, dispositivo real, persistencia, biblioteca o servicio externo.

La ubicación y duración del estado compartido deben respetar el ciclo de vida elegido para el simulador: las consultas y órdenes relacionadas deben observar el mismo estado. Se debe acordar el ciclo de vida DI adecuado al uso del host antes de implementarlo; no se presupone persistencia entre reinicios ni coordinación entre instancias.

Los tests solicitados por requisitos quedan pendientes de resolución por el conflicto documentado. No se modificarán pruebas arquitectónicas.

### 4. Presentation

No requiere componentes ni lógica de presentación: no se solicita una interfaz de usuario para operar el simulador. El host solo utilizará el registro de dependencias existente para componer la implementación de Infrastructure. La UI no accederá directamente a la clase simulada.

## Verificación prevista

Una vez resueltas las aclaraciones y el conflicto de pruebas, verificar los comportamientos solicitados: consulta por nodo; variación de potencia según hora; activación y desactivación; cero generación mientras el panel esté desactivado; cambio y consulta del estado de volcado; y respuesta ante fallas configuradas. Además, comprobar que la composición resuelve `IInversorClient` y ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` sin modificar sus pruebas.
