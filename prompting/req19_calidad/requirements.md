# Requerimiento: Calidad de software y pruebas automatizadas — Entrega 3

## Objetivo

Mejorar y verificar la calidad del código del proyecto SolarUY mediante pruebas automatizadas, desarrollo guiado por pruebas (TDD), análisis estático y seguimiento de los indicadores de calidad.

## Requerimientos funcionales y técnicos

### 1. Pruebas unitarias del Dominio

Completar y mantener las pruebas unitarias con xUnit para las entidades y reglas de negocio del Dominio:

- `ParqueSolar`.
- `Nodo`.
- `Panel`.
- `LecturaPanel`.
- `Mantenimiento` (respetando el nombre y modelo existentes en el proyecto).

Las pruebas deben contemplar, según corresponda:

- Creación válida de entidades.
- Rechazo de datos inválidos.
- Validaciones de obligatoriedad y límites.
- Reglas de negocio.
- Transiciones de estado.
- Relaciones y restricciones entre entidades.
- Preservación de invariantes durante las operaciones de modificación.

Aplicar TDD: escribir primero una prueba que reproduzca el comportamiento esperado, comprobar que falla cuando corresponde, implementar o corregir el comportamiento y verificar que la prueba pase.

No duplicar pruebas ni inventar reglas de negocio que no estén definidas.

### 2. Análisis estático de calidad

Ejecutar periódicamente el script `Analizar-Calidad.ps1`, como mínimo antes de cada entrega.

Revisar los resultados de Sonar y corregir los problemas identificados en las categorías:

- Security.
- Reliability.
- Maintainability.

Priorizar los problemas según su severidad y documentar aquellos que no puedan corregirse, explicando su justificación.

No ocultar problemas mediante supresiones injustificadas ni modificar la configuración de análisis para evitar que se detecten.

### 3. Integridad de las pruebas de arquitectura

Verificar que los tests de arquitectura existentes continúen pasando.

- No modificar las pruebas estructurales para hacer que una implementación incorrecta pase.
- No eliminar restricciones arquitectónicas para resolver errores.
- Revisar los cambios realizados en los tests de arquitectura y justificar cualquier modificación legítima.
- Corregir el código de producción cuando incumpla las reglas arquitectónicas existentes.

### 4. Documentación de la evolución de calidad

Preparar una nota breve para la defensa que incluya:

- Evolución de los indicadores de calidad de Sonar.
- Capturas de los resultados relevantes.
- Principales problemas encontrados.
- Correcciones realizadas.
- Resultados de las pruebas automatizadas.
- Problemas pendientes, si existen.

Las capturas y los resultados deben corresponder a ejecuciones reales del análisis.

## Criterios de aceptación

- El Quality Gate de Sonar está en verde para la entrega 3.
- `dotnet test` finaliza correctamente.
- Las pruebas unitarias del Dominio cubren las reglas de negocio relevantes de las cinco entidades indicadas.
- Los tests de arquitectura existentes continúan pasando sin modificaciones injustificadas.
- El script `Analizar-Calidad.ps1` se ejecuta correctamente y se revisan los problemas de Security, Reliability y Maintainability.
- Existe una nota documentada sobre la evolución de calidad, con capturas reales de Sonar.

## Restricciones

- Respetar Clean Architecture y las reglas arquitectónicas existentes.
- Mantener xUnit como framework de pruebas existente.
- No introducir dependencias nuevas sin justificación y autorización.
- No cambiar las reglas de negocio para hacer pasar las pruebas.
- No modificar los tests estructurales para ocultar incumplimientos.
- No declarar pruebas, análisis o resultados como satisfactorios sin haberlos ejecutado.

## Estado de verificación

Las convenciones y proyectos de pruebas vigentes, y el script de análisis utilizado, están identificados en `implementations.md`. La última ejecución intentada de Sonar no llegó a publicar un análisis de esta entrega; por tanto, el Quality Gate y los indicadores actuales siguen pendientes de verificación. No se usan resultados históricos como evidencia de esta entrega.
