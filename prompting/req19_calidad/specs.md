# Especificaciones: Calidad de software y pruebas automatizadas

## Alcance

Mantener la calidad de SolarUY mediante pruebas xUnit de Domain, controles arquitectónicos y análisis estático con el script y la instancia Sonar existentes. No agregar reglas de negocio para elevar cobertura ni cambiar los guardarraíles para ocultar incumplimientos.

## Domain

- Las pruebas cubren las reglas existentes de `ParqueSolar`, `Nodo`, `Panel`, `LecturaPanel` y `TareaMantenimiento`.
- Los casos cubren construcción válida e inválida, límites, relaciones, invariantes y transiciones que ya define el dominio.
- Aplicar TDD cuando se modifique lógica de Domain. No duplicar casos ni inferir reglas no documentadas.

## Application, Infrastructure y Presentation

Se ejecutan las suites de Application e integración. La cobertura adicional del Quality Gate se obtiene con pruebas de integración de autenticación y permisos, directorio de técnicos, gestión de inventario, mantenimiento, monitoreo, simulador y reportes.

Las pruebas de pantallas ejecutan los callbacks de Blazor y Radzen con los servicios y repositorios de la aplicación y una MariaDB aislada. Las pruebas de autenticación usan respuestas controladas de OpenID Connect y Keycloak. Los recorridos en navegador se verifican con su propia suite.

## Análisis estático y arquitectura

- Ejecutar `Analizar-Calidad.ps1` desde la raíz y revisar Security, Reliability y Maintainability.
- Corregir problemas reproducibles sin supresiones injustificadas ni cambios a la configuración para ocultarlos.
- Ejecutar `CodeQuality.Tests` sin modificar sus restricciones para acomodar el código.
- Registrar por separado análisis publicados, intentos incompletos, pruebas aprobadas, fallidas y omitidas. Un resultado histórico de Sonar no acredita una ejecución actual.

## Documentación y aceptación

Registrar en `implementations.md` los comandos, resultados y límites reales. La aceptación de Sonar requiere un análisis publicado de la entrega con Quality Gate aprobado; no se infiere de un análisis anterior ni de un intento interrumpido.


## Ejecución reproducible

El analizador usa una copia temporal dentro del contenedor: no comparte binarios, obj ni análisis anteriores con el desarrollo Windows. Compila en serie, genera cobertura OpenCover de Domain, Application e integración y espera el Quality Gate existente. El token solo se recibe por entorno y no se modifican las condiciones del servidor. La copia de fuentes se monta en solo lectura, el análisis se ejecuta sin root y los resultados se exportan a una carpeta única bajo `artifacts/calidad/`. No se incorporan perfiles temporales del navegador ni coberturas anteriores.
