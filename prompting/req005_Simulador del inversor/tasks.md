# Tareas de implementación: Simulador del inversor

## 1. Aclaraciones previas

- [ ] 1.1 Confirmar los identificadores de nodo/panel existentes y los datos requeridos en las consultas de estado y potencia.
- [ ] 1.2 Acordar firmas, tipos de retorno, semántica de errores y uso síncrono o asíncrono de `IInversorClient`.
- [ ] 1.3 Definir rango de potencia, horas de luz y límites de la curva solar diaria.
- [ ] 1.4 Definir parámetros, alcance y efectos observables de las fallas simuladas, de forma que puedan configurarse determinísticamente.
- [ ] 1.5 Acordar el ciclo de vida DI del simulador para preservar coherencia del estado en las operaciones relacionadas.
- [ ] 1.6 Resolver cómo verificar la lógica del simulador respetando el requisito de pruebas y la regla del proyecto que prohíbe pruebas unitarias para Infrastructure.

## 2. Application

- [ ] 2.1 Definir `IInversorClient` con las seis operaciones y los contratos acordados para consultas por nodo, potencia, activación/desactivación de panel y volcado a red.

## 3. Infrastructure

- [ ] 3.1 Implementar el adaptador simulado de `IInversorClient` con estado de paneles y volcado coherente durante el ciclo de vida acordado.
- [ ] 3.2 Implementar el cálculo de potencia diario dentro del rango y horario acordados, excluyendo paneles desactivados.
- [ ] 3.3 Implementar estado de salud y generación configurable de fallas conforme al comportamiento aprobado.
- [ ] 3.4 Registrar la implementación simulada en la composición de dependencias existente de Infrastructure.
- [ ] 3.5 Incorporar la verificación de los comportamientos aprobados por el mecanismo que resuelva la aclaración 1.6, sin alterar las pruebas arquitectónicas.

## 4. Presentation

- [ ] 4.1 Confirmar que el host resuelve el puerto mediante el registro de Infrastructure existente, sin agregar UI ni dependencia directa de la implementación concreta.

## 5. Verificación

- [ ] 5.1 Ejecutar `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` y confirmar que los guardarraíles arquitectónicos pasan sin modificar sus pruebas.

## 6. Cierre y documentación

- [ ] 6.1 Crear el archivo `implementations.md` en la misma carpeta detallando los cambios realizados, archivos tocados y resultado de las pruebas.
