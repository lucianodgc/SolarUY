# Implementación: REQ015 — Reportes con gráficos del histórico y exportación

Fecha de cierre: 12/11/2026. **Las 47 tareas de `tasks.md` están completas.** Se reutilizó la funcionalidad existente, se corrigió una condición de carrera y se ajustó la legibilidad del resumen de mantenimiento. La regresión final terminó con **241 pruebas correctas, 0 fallidas y 4 omitidas explícitamente**. Se completó la inspección de Chrome con los tres roles y la descarga real de los CSV.

## Resultado funcional

La pantalla `/reportes` presenta generación histórica en kWh mediante barras y líneas Radzen, filtros de parque/nodo/período/fechas, detalle de cobertura e intervalos, mantenimientos realizados y exportación CSV del resultado vigente.

Se conservan las vistas Diario/Mensual/Anual/Rango del histórico, la zona `America/Montevideo`, los límites `[inicio, fin)`, la precisión original y la distinción entre cero medido y ausencia de datos. Las lecturas sin nodo histórico conocido no se atribuyen a la topología actual; el diagnóstico conserva su alcance global al período.

Los tres roles leen y exportan reportes. La consulta del resumen no amplía los permisos para gestionar mantenimiento.

## Cambios realizados

### Domain, Application e Infrastructure

Se revisaron y reutilizaron las entidades, contratos y servicios existentes de req004, req007 y req008. No se añadieron entidades, atributos, migraciones, paquetes ni cálculos históricos.

- `ReporteHistoricoService`: conserva autorización en opciones/consulta/exportación, validación de pertenencia del nodo y coordinación de histórico y mantenimiento.
- `HistoricoGeneracionService` y `HistoricoGeneracionRepository`: siguen siendo la fuente única de fechas, agrupaciones, energía, cobertura y procedencia.
- `ResultadoReporteDto` y `GraficoReporteDto`: conservan los resultados en servidor, colecciones de solo lectura y segmentos separados por los huecos.
- `TareaMantenimientoRepository.ListarRealizadosParaReporteAsync`: consulta Completadas con realización no nula dentro del período, filtrando por objetivo mediante las relaciones persistidas; conserva lectura sin seguimiento, proyección y contexto por operación.
- `ReporteCsv`: conserva las 24 columnas, generación/mantenimiento/advertencia, intervalos sin datos, precisión, UTF-8 sin BOM, CRLF, escape y fechas ISO 8601 con desplazamiento de Uruguay. La exportación no vuelve a consultar lecturas.

La revisión no encontró una definición funcional pendiente que requiriera otra decisión del usuario.

### Presentation

En `ReportesHistorico.ActualizarAsync`, la cancelación anterior se esperaba antes de registrar la nueva consulta e invalidar el resultado. Con una cancelación demorada y tres selecciones sucesivas, una solicitud anterior podía reanudarse después de la última, borrar su resultado y dejar carga/exportación en un estado incorrecto.

Ahora se registra la nueva cancelación y se invalida el resultado antes de esperar. Al reanudarse se comprueba que la selección siga vigente. Se conserva el descarte de respuestas tardías y la cancelación al desechar la página.

Durante la inspección se detectó que las siete columnas del mantenimiento se comprimían excesivamente en una ventana angosta. Se asignaron anchos definidos para conservar encabezados y datos legibles mediante desplazamiento horizontal. Se verificaron visualmente el desplazamiento y la fecha de realización completa.

Se reutilizaron `GraficoGeneracionReporte.razor`, `ResultadoHistorico.razor`, `reportes.js`, el registro de servicios y la navegación existentes.

### Integraciones y servidor de inspección

Se añadieron a `ReportesConcurrenciaIntegrationTests` dos variantes del escenario con cancelación demorada: una con repositorios reales/MariaDB y otra que integra página, Radzen y ambos servicios reales con repositorios controlados. Comparten las mismas aserciones. Se comprobó que la nueva regresión falla con la secuencia original y pasa con la corrección; el fallo esperado de esa comprobación temporal no pertenece al código final.

`ReportesPantallaIntegrationTests` añade HTTP real de la pantalla para Administrador/Tecnico/Consulta, rechazo de anónimo/Invitado y disponibilidad de recursos Radzen, Blazor y descarga. También proporciona el servidor temporal de inspección, limitado a loopback, con cookies de pruebas y datos conocidos. Sus rutas de ingreso no se incorporan a Presentation ni sustituyen el login de producción.

## Archivos tocados

| Archivo | Cambio |
| --- | --- |
| `SolarUY.Presentation/Components/Pages/ReportesHistorico.razor` | Corrección de concurrencia y anchos de columnas del mantenimiento. |
| `SolarUY.Integration.Tests/ReportesConcurrenciaIntegrationTests.cs` | Regresión de cancelación demorada, con y sin MariaDB. |
| `SolarUY.Integration.Tests/ReportesPantallaIntegrationTests.cs` | Integración HTTP y servidor temporal de inspección visual. |
| `SolarUY.Integration.Tests/README.md` | Preparación y límites de las pruebas, inspección y dependencia del simulador para la regresión completa. |
| `prompting/req015_Reportes_con_gráficos_del_histórico/specs.md` | Variante complementaria de concurrencia sin MariaDB y legibilidad de columnas. |
| `prompting/req015_Reportes_con_gráficos_del_histórico/tasks.md` | Cierre de las tareas con evidencia. |
| `prompting/req015_Reportes_con_gráficos_del_histórico/implementations.md` | Informe final. |

`requirements.md` se conserva. No se modificaron secretos, recolector, despliegue, guardarraíles de `CodeQuality.Tests` ni las carpetas protegidas de reglas y habilidades.

## Entorno de verificación

- Windows, SDK .NET 8.0.425 y runtime .NET 8.0.31.
- Contenedor exclusivo `solaruy-req015-test`, imagen `mariadb:11.8`, puerto `127.0.0.1:13315`, credencial temporal fuera del código.
- Base de regresión `solaruy_req007_test_req015` y base visual separada `solaruy_req007_test_req015_ui`. No se utilizó la base habitual.
- Simulador TCP del proyecto en `127.0.0.1:5055` para las pruebas existentes de monitoreo y recolección.
- Compilación con un nodo MSBuild y sin servidor compartido. Se usaron salidas aisladas para evitar binarios bloqueados por procesos previos.

Al terminar se cerraron ambos servidores visuales y se detuvo el simulador temporal. El contenedor de pruebas quedó detenido, conservando sus bases para reproducir la verificación; puede iniciarse con `docker start solaruy-req015-test`.

## Pruebas automatizadas

La selección de reportes, procedencia y pantalla con MariaDB terminó con **38 correctas, 0 fallidas y 1 omitida**; la omitida es exclusivamente el servidor de inspección manual. Las teorías con datos sí expandieron y ejecutaron sus casos. El archivo de resultados es `artifacts/req015/tests/req015-mariadb_net8.0_20261112014531.trx`.

La primera regresión completa con MariaDB encontró seis fallos de monitoreo/recolección por ausencia del simulador TCP. Se inició el simulador y se repitió sin modificar ni relajar pruebas: desaparecieron los seis fallos. Tras el ajuste de columnas se compiló y ejecutó nuevamente toda la solución.

Comando final, con la conexión aislada cargada en el proceso y el simulador disponible:

```powershell
dotnet test SolarUY.sln --no-restore -m:1 -p:UseSharedCompilation=false -p:BaseOutputPath=bin/req015-cierre-final/ --logger 'trx;LogFilePrefix=req015-final-verificada' --results-directory artifacts/req015/tests --logger 'console;verbosity=minimal'
```

| Proyecto | Correctas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| CodeQuality.Tests | 9 | 0 | 0 |
| SolarUY.Domain.Tests | 57 | 0 | 0 |
| SolarUY.Application.Tests | 16 | 0 | 0 |
| SolarUY.Integration.Tests | 159 | 0 | 4 |
| **Total final** | **241** | **0** | **4** |

Las cuatro omisiones son dos servidores manuales (`MonitoreoHubIntegrationTests.Pantalla_ServidorTemporalParaVerificacionEnNavegador` y `ReportesPantallaIntegrationTests.Pagina_ServidorTemporalParaVerificacionEnNavegador`) y los dos escenarios de `MonitoreoNginxIntegrationTests` retirados por la arquitectura actual. No son pruebas de reportes con MariaDB omitidas. El servidor manual de reportes sí se ejecutó separadamente y se cerró correctamente en ambas inspecciones.

Se ejecutaron las regresiones de concurrencia, procedencia y conservación de lecturas, CSV, mantenimiento, permisos y la prueba existente de un millón de lecturas/cuatro consultas concurrentes. El caso de volumen pasó sin alterar sus umbrales. Esto valida el entorno de pruebas registrado, no otros despliegues.

Evidencia final en `artifacts/req015/tests/`:

- `req015-final-verificada_net8.0_20261112020127.trx`: arquitectura.
- `req015-final-verificada_net8.0_20261112020129.trx`: Domain.
- `req015-final-verificada_net8.0_20261112020130.trx`: Application.
- `req015-final-verificada_net8.0_20261112020330.trx`: integración completa.
- `req015-navegador_net8.0_20261112020106.trx` y `req015-navegador-final_net8.0_20261112020438.trx`: servidores de inspección, ambos correctos.

## Recorrido visual y CSV descargado

Se utilizó Chrome mediante control nativo de la interfaz, con identidades controladas del servidor de pruebas para los tres roles. Se inspeccionaron capturas de ventana y el árbol de accesibilidad; no se sustituyó la descarga real por una serialización fuera del navegador.

| Comprobación | Resultado observado |
| --- | --- |
| Administrador, Tecnico y Consulta | Los tres acceden, muestran la generación y descargan el CSV. |
| Barras y líneas | Mismos valores; tres puntos separados, sin líneas que atraviesen los huecos. Ejes de energía en kWh y tiempo de Uruguay. |
| Datos del parque | 0,700 kWh: intervalos de 0,600, 0,100 y 0,000; 21 intervalos sin datos y cobertura parcial. |
| Día, mes, año y rango | Total de 0,700; mes con límites 01/11–01/12, año 01/01/2026–01/01/2027 y rango 12/11–13/11. |
| Rango inválido | Inicio igual a fin: mensaje de validación, resultados anteriores retirados y exportación deshabilitada. |
| Día sin información | 13/11: mensaje de ausencia de generación y mantenimiento, sin sustituir energía ausente por cero. |
| Nodo y parque | Nodo uno: 0,400 kWh y un mantenimiento; cambiar a Otro parque limpia el nodo, muestra 1,000 kWh y ningún mantenimiento. |
| Mantenimiento | Dos Completadas en el parque histórico; descripción con separador/comillas/ñ; fechas en Uruguay y columnas accesibles por desplazamiento horizontal. |
| Permisos de gestión | Consulta no muestra el acceso de gestión y recibe HTTP 403 al navegar a `/mantenimiento`. |
| Procedencia | Aviso de una lectura sin nodo verificado, sin sumarla a los agregados ni atribuirla al parque seleccionado. |

La carga, cancelación demorada y respuestas fuera de orden se comprobaron adicionalmente mediante las integraciones de la página real, con repositorios reales y con la variante controlada. No se atribuye al recorrido visual una inyección de fallos de transporte o una prueba de login OIDC en producción.

Los CSV descargados por los tres roles se conservaron en:

- `artifacts/req015/browser/administrador.csv`
- `artifacts/req015/browser/tecnico.csv`
- `artifacts/req015/browser/consulta.csv`

Son idénticos byte a byte, con **9827 bytes** y SHA-256 `8B210AB7E396B48FE24A7EB2F5DE7BAFDF7146FF409E712CCB5E154EBFB2AB91`. Se verificaron UTF-8 sin BOM, 24 columnas, 24 intervalos, 21 huecos con energía vacía, cero medido, dos mantenimientos, una advertencia, escape de caracteres y total 0,7 kWh coincidente con la pantalla. La equivalencia con el histórico, conservación del resultado al incorporarse nuevas lecturas y ausencia de escrituras también pasaron en las integraciones.

## Cierre

Se comprobaron `git diff --check`, UTF-8 sin BOM y coherencia entre requisitos, especificación y tareas. No quedan tareas de REQ015 pendientes. Las omisiones explícitas de la suite general se conservan documentadas y no se contabilizan como pruebas aprobadas.
