# Tareas de implementación: Simulador del inversor

## 1. Aclaraciones previas

- [x] 1.1 Confirmar identificadores de texto y DTOs de estado/potencia; configurar el mapa de nodos y paneles, con topología simulada predeterminada.
- [x] 1.2 Definir operaciones asíncronas con `CancellationToken`, tipos de retorno y `KeyNotFoundException` para identificadores desconocidos.
- [x] 1.3 Definir potencia en watts, curva sinusoidal de 06:00 a 18:00 locales y máximo de 400 W por panel.
- [x] 1.4 Configurar fallas mediante identificadores de panel; los paneles fallados reportan salud `Fallado` y potencia cero.
- [x] 1.5 Registrar el simulador como singleton por proceso; el estado no persiste entre reinicios ni se coordina entre instancias.
- [x] 1.6 Incorporar la verificación permanente pedida explícitamente por el usuario mediante pruebas de integración en `SolarUY.Integration.Tests`: xUnit, composición real de Application/Infrastructure, reloj e identidad controlados y sin nuevas pruebas unitarias de Infrastructure.

## 2. Application

- [x] 2.1 Definir `IInversorClient` con consultas de estado y potencia, operaciones de panel y volcado, y consulta del estado del volcado.

## 3. Infrastructure

- [x] 3.1 Implementar el adaptador simulado con estado coherente por panel y estado de volcado.
- [x] 3.2 Implementar el cálculo de potencia diario, excluyendo paneles desactivados.
- [x] 3.3 Implementar estado de salud y fallas configurables.
- [x] 3.4 Registrar la implementación simulada en la composición de dependencias existente de Infrastructure.
- [x] 3.5 Incorporar y ejecutar la suite permanente: 19 casos de integración para topología, potencia según hora/zona, fallas, activar/desactivar, aislamiento por panel/nodo, volcado compartido entre ámbitos, permisos, entradas inválidas y cancelación. Se ejecuta con `dotnet test SolarUY.sln`.

## 4. Presentation

- [x] 4.1 Registrar el puerto desde `AddSolarInfrastructure`, sin referencias de Application/Domain a la clase concreta.
- [x] 4.2 Incorporar `/simulador`, acceso desde inicio/menú, selector de nodos, potencia/estado de paneles y volcado, controles de actualización y mensajes de carga/error.
- [x] 4.3 Incorporar casos de uso de Application que validan identidad/roles: Administrador opera; Técnico y Consulta solo consultan, según confirmación del usuario.
- [x] 4.4 Enumerar la topología configurada desde `IInversorClient` y mantener la UI independiente del adaptador y del inventario real.

## 5. Verificación

- [x] 5.1 Ejecutar las pruebas de `CodeQuality.Tests` reconstruyendo la aplicación en una salida separada: los 9 guardarraíles pasan sin modificar sus pruebas y el host compila con App.razor restaurado.

## 6. Cierre y documentación

- [x] 6.1 Crear el archivo `implementations.md` en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
