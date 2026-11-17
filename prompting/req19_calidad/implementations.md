# Calidad de software y pruebas automatizadas — Entrega 3

## Ejecuciones reales

Fecha de verificación: 17 de noviembre de 2026.

| Comprobación | Resultado |
| --- | --- |
| `dotnet restore SolarUY.sln` | Correcto. Se restauraron los nueve proyectos. |
| `dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj` | 77 aprobadas, 0 fallidas. |
| Cobertura xUnit del Dominio | 97,4 % de líneas (488/501) y 84,76 % de ramas (256/302), en `SolarUY.Domain.Tests/TestResults/74f6886d-d46a-46ea-b01c-ae4a50639bd8/coverage.cobertura.xml`. |
| `dotnet test SolarUY.Application.Tests/SolarUY.Application.Tests.csproj` | 16 aprobadas, 0 fallidas, como parte de la ejecución de solución. |
| `dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj` | 125 aprobadas, 0 fallidas y 119 omitidas por depender de servicios, credenciales o base aislada no configurados. |
| `dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj` | 9 aprobadas, 0 fallidas. No se modificaron los tests de arquitectura. |

Las pruebas de Domain existentes cubren las entidades solicitadas: `ParqueSolar`, `Nodo`, `Panel`, `LecturaPanel` y `TareaMantenimiento`, incluyendo creación, datos inválidos, transiciones, relaciones e invariantes. No se agregaron reglas de negocio nuevas únicamente para elevar cobertura.

## SonarQube

Se ejecutó el script existente `../SonarQube/analizar.sh` contra el proyecto `tallerNETSonarDemo`. El script llegó a iniciar SonarScanner, restaurar dependencias y comenzar la compilación, pero el contenedor se interrumpió antes de `sonarscanner end`; por ello no publicó un análisis nuevo.

Se repitió el análisis con compilación serializada y con el caché NuGet local montado en solo lectura. Este intento llegó a analizar Domain, Application e Infrastructure, pero también se interrumpió antes de completar Presentation y de publicar. Los avisos observados fueron de mantenibilidad (por ejemplo complejidad cognitiva, clases marcadoras vacías, literales repetidos y `TODO`); no se ocultaron mediante supresiones ni se alteró la configuración de Sonar.

La API de SonarQube informó que el último análisis publicado sigue siendo el 8 de octubre de 2026. Ese análisis histórico tiene Quality Gate `OK`, Security `A`, Reliability `A` y cero violaciones bloqueantes, pero **no acredita el estado de esta entrega**. No se agregaron capturas porque el requerimiento exige que correspondan a una ejecución publicada real.

## Pendientes para cierre de Sonar

1. Estabilizar el contenedor de análisis hasta que complete `sonarscanner end` y se publique una fecha nueva.
2. Revisar y corregir los avisos de mantenibilidad encontrados en la ejecución completa, sin cambiar reglas de negocio ni ocultarlos.
3. Capturar desde la interfaz de SonarQube el Quality Gate y los indicadores de Security, Reliability y Maintainability de esa ejecución nueva.

## Archivos de arquitectura

No se editaron archivos de `CodeQuality.Tests`. La ejecución vigente confirma que sus nueve restricciones permanecen aprobadas.
