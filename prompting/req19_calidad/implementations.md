# Implementación: calidad y correcciones verificadas

## Verificación vigente

Se completó la compilación y las cuatro suites de la solución sin modificar CodeQuality.Tests ni archivos protegidos. Resultado del análisis final reproducible:

| Suite | Aprobadas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| Arquitectura | 9 | 0 | 0 |
| Domain | 77 | 0 | 0 |
| Application | 16 | 0 | 0 |
| Integración | 318 | 0 | 14 |
| Total | 420 | 0 | 14 |

Las omisiones corresponden a proveedores IA reales, servidores para inspección y pruebas que necesitan proxy/navegador configurados. Se ejercitaron aparte cuatro casos antes bloqueantes, incluidos los servidores temporales de chatbot/reportes con rechazo 401 sin identidad; todos aprobaron. Esto no acredita un nuevo recorrido visual completo ni login OIDC real.

También se validaron Docker Compose, construcción de las imágenes web/simulador, sintaxis de los scripts PowerShell y git diff --check. La restauración Windows informó NU1900 por indisponibilidad del índice de vulnerabilidades; no se desactivó esa comprobación. La restauración y compilación final Linux del analizador terminaron correctamente.

## SonarQube publicado

El analizador anterior se interrumpía y solo había evidencia histórica. Se añadió Analizar-Calidad.ps1 con una imagen reproducible: copia fuentes en un entorno Linux separado, compila en serie, ejecuta las suites, importa cobertura OpenCover de Domain/Application/integración y espera el Quality Gate. El análisis se ejecuta sin root, recibe el token por entorno, monta las fuentes en solo lectura y exporta resultados a una carpeta única. Se excluyen perfiles generados del navegador y artefactos anteriores.

Se corrigieron cuatro avisos bloqueantes con aserciones explícitas verificadas. Dos avisos SQL S2077 se revisaron y marcaron como falsos positivos en Sonar: las listas contienen nombres de parámetros generados internamente; todos los valores de consulta siguen siendo parámetros ligados. No se alteraron condiciones del Gate, línea base, pruebas arquitectónicas ni fórmulas para aprobarlo.

Último análisis publicado: proyecto [SolarUY](http://localhost:9000/dashboard?id=SolarUY), identificador 57c3a1c3-926b-46bf-9809-4a9284a7fcd9.

| Indicador | Resultado |
| --- | --- |
| Quality Gate | ERROR |
| Security / Reliability / Maintainability | A / A / A |
| Bloqueantes | 0 |
| Cobertura global | 66,2 % |
| Cobertura de código nuevo | 66,4 %; exige 80 % |
| Duplicación de código nuevo | 0,36 %; máximo 3 % |
| Hallazgos nuevos | 138; exige 0 |

La cobertura faltante y los hallazgos de mantenibilidad mantienen el Gate rojo. Incluyen complejidad de métodos, bloques vacíos, expresiones anidadas y convenciones; requieren correcciones y verificación adicionales, sin ocultarlos ni relajar límites. No se declara completado el cierre de calidad.

## Evidencia y reproducción

- Ejecución final: `artifacts/calidad/6073c48e30e84c11aaa9abd1b058a600/`, con TRX, tres colecciones OpenCover y report-task.txt.
- Pruebas nativas de volumen: `artifacts/correcciones/cierre/`; inventario/permisos: `artifacts/correcciones/paneles-final/`; controles arquitectónicos: `artifacts/correcciones/arquitectura-final/`; casos bloqueantes: `artifacts/correcciones/bloqueantes/`.
- Repetir: definir SONAR_TOKEN y, para MariaDB, SOLARUY_HISTORICO_TEST_CONNECTION hacia una base desechable solaruy_req007_test..., y ejecutar `./Analizar-Calidad.ps1`. El token nunca se escribe en archivos versionados.

Los artefactos locales están ignorados por Git. No se restableció ni migró la base habitual. Quedan pendientes corregir el backlog de calidad y obtener las capturas del panel requeridas por tasks.md.
