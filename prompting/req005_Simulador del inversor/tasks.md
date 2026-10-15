# Tareas de implementación: Simulador del inversor

## 1. Aclaraciones previas

- [x] 1.1 Confirmar identificadores de texto y DTOs de estado/potencia; configurar el mapa de nodos y paneles, con topología simulada predeterminada.
- [x] 1.2 Definir operaciones asíncronas con `CancellationToken`, tipos de retorno y `KeyNotFoundException` para identificadores desconocidos.
- [x] 1.3 Definir potencia en watts, curva sinusoidal de 06:00 a 18:00 locales y máximo de 400 W por panel.
- [x] 1.4 Configurar fallas mediante identificadores de panel; los paneles fallados reportan salud `Fallado` y potencia cero.
- [x] 1.5 Registrar el simulador como singleton por proceso; el estado no persiste entre reinicios ni se coordina entre instancias.
- [ ] 1.6 Resolver cómo verificar la lógica del simulador respetando el requisito de pruebas y la regla del proyecto que prohíbe pruebas unitarias para Infrastructure.

## 2. Application

- [x] 2.1 Definir `IInversorClient` con consultas de estado y potencia, operaciones de panel y volcado, y consulta del estado del volcado.

## 3. Infrastructure

- [x] 3.1 Implementar el adaptador simulado con estado coherente por panel y estado de volcado.
- [x] 3.2 Implementar el cálculo de potencia diario, excluyendo paneles desactivados.
- [x] 3.3 Implementar estado de salud y fallas configurables.
- [x] 3.4 Registrar la implementación simulada en la composición de dependencias existente de Infrastructure.
- [ ] 3.5 Incorporar la verificación de los comportamientos aprobados por el mecanismo que resuelva la aclaración 1.6, sin alterar las pruebas arquitectónicas.

## 4. Presentation

- [x] 4.1 Registrar el puerto desde `AddSolarInfrastructure`; no se agregaron UI ni referencias de Application/Domain a la clase concreta.

## 5. Verificación

- [x] 5.1 Ejecutar las pruebas de `CodeQuality.Tests` sin reconstruir las referencias (`BuildProjectReferences=false`); los 9 guardarraíles pasan sin modificar sus pruebas. La compilación integral sigue bloqueada por el `App.razor` ausente e ignorado por Git.

## 6. Cierre y documentación

- [x] 6.1 Crear el archivo `implementations.md` en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
