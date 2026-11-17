# Implementación: Reportes con gráficos del histórico y exportación

Presentación — 17/11/2026: se separa el encabezado de cada período mediante la clase reporte-periodo y todos los selectores de fecha incluyen hora de 24 horas. Se aclara que Rango respeta horas y fin exclusivo, mientras Diario/Mensual/Anual conservan períodos completos. Se reutilizan los cálculos existentes. Compilación, arquitectura e integraciones de reportes aprobadas junto con la verificación de monitoreo; revisión visual pendiente.

Actualización del 29/10/2026: histórico, gráficos y CSV respetan el nodo histórico guardado; las lecturas legadas de nodo desconocido permanecen por panel y generan un aviso, sin asignarlas al nodo/parque actual. Ver [informe de reparación y pruebas](../Reportes%20de%20codex/reparacion_migraciones_20261029.md). La evidencia del 22/10 siguiente corresponde a la implementación anterior.

Fecha: 22/10/2026. La pantalla está disponible en `/reportes` para Administrador, Tecnico y Consulta. Reutiliza el histórico de req7, agrega el resumen de mantenimiento realizado y permite descargar ambos conjuntos de datos en un único CSV.

## Comportamiento implementado

- Filtros por parque, nodo opcional, período diario/mensual/anual o rango y fechas de Uruguay. Los cambios consultan automáticamente; cambiar de parque limpia la selección de nodo. La pertenencia se valida también en Application.
- Gráficos Radzen de barras y líneas sobre los valores del histórico, con unidad kWh, eje temporal de Uruguay y escala vertical legible para valores menores a 1 kWh. Cambiar la representación no consulta de nuevo.
- Los puntos sin energía se conservan como ausencia de datos en el CSV y no se convierten en cero. Las líneas se separan en segmentos para no atravesar huecos; se conservan los ceros medidos y los avisos de cobertura parcial.
- Mantenimientos exclusivamente Completados cuya FechaRealizacion pertenece a `[inicio, fin)`. Se excluyen completados sin fecha; se muestran panel, tipo, descripción, responsable, estado y las dos fechas. Los campos proceden de los registros existentes, sin duplicarlos.
- CSV con separador punto y coma, UTF-8 sin BOM, fechas ISO 8601 con desplazamiento de Uruguay y decimales invariantes sin redondeo adicional. `TipoRegistro` distingue Generacion/Mantenimiento; incluye todos los intervalos, incluso los que tienen energía vacía y cobertura SinDatos. Los encabezados y sus 24 columnas están registrados en Q2 de `specs.md`.
- Exportación del mismo resultado generado en el servidor que alimenta la pantalla, con nueva comprobación de identidad y rol. No vuelve a consultar las lecturas al descargar: el archivo conserva los valores aunque el recolector haya guardado muestras nuevas.
- Carga, validaciones, inexistencia, denegación, ausencia de energía/tareas y errores visibles. Los filtros nuevos invalidan el resultado previo y deshabilitan su exportación. La cancelación y un contador de versión impiden que una respuesta tardía sustituya a la selección vigente.

Q1 y Q2 fueron respondidas por el usuario durante la implementación y sus respuestas quedaron registradas en `specs.md`.

## Cambios por capa y archivos

Domain no cambia: no se añadieron entidades, atributos, estados, relaciones ni migraciones.

Application:

- `SolarUY.Application/Reportes/FiltroReporteDto.cs`: filtro de parque/nodo, período y fechas.
- `ContratosReportes.cs`: contratos del servicio, consulta y resumen de mantenimiento, archivo CSV y resultado de reporte con colecciones de solo lectura.
- `ReporteHistoricoService.cs`: autorización, opciones, pertenencia parque/nodo y coordinación del histórico con mantenimiento. Delega toda la energía y resolución temporal en `IHistoricoGeneracionService`.
- `GraficoReporteDto.cs`: adaptación de puntos y segmentos para Radzen, conservando los valores originales.
- `ReporteCsv.cs`: serialización y escape del resultado, sin archivos persistentes en servidor ni bibliotecas adicionales.
- `SolarUY.Application/Mantenimiento/ITareaMantenimientoRepository.cs`: nueva operación específica de consulta para reportes.

Infrastructure:

- `SolarUY.Infrastructure/Persistence/TareaMantenimientoRepository.cs`: nueva proyección SQL de solo lectura, con filtros de parque/nodo, estado y fecha de realización, contexto por operación, cancelación y orden estable.
- Las restricciones por responsable y las operaciones de gestión de mantenimiento mantienen su comportamiento. No se modificaron el SQL del histórico, índices, recolector ni configuración de despliegue.

Presentation:

- `SolarUY.Presentation/Components/Pages/ReportesHistorico.razor`: filtros, estado de consulta, resumen, exportación y manejo de concurrencia.
- `Components/Shared/GraficoGeneracionReporte.razor`: gráficos Radzen con selector de representación.
- `Components/Layout/NavMenu.razor`: acceso a Reportes para los tres roles.
- `Program.cs`: registro del servicio de reportes con el ámbito de circuito existente.
- `wwwroot/js/reportes.js`: descarga del contenido autorizado y liberación de la URL temporal del navegador.

Verificación y documentación:

- `SolarUY.Integration.Tests/ReportesIntegrationTests.cs`: 27 casos de servicios/repositorios con MariaDB real.
- `ReportesConcurrenciaIntegrationTests.cs`: un caso del componente real y HtmlRenderer con servicios y persistencia reales.
- `SolarUY.Integration.Tests/README.md`: preparación y ejecución de los 28 casos.
- `specs.md`, `tasks.md` y este informe en la carpeta de req8. `requirements.md` se conserva sin cambios.

## Verificaciones

### Integración de reportes

Los **28 casos nuevos pasan**, sin omisiones. Cubren los cuatro períodos por parque/nodo, valores conocidos y comparación con el histórico, cero/huecos, límites y pertenencia, mantenimiento por fecha de realización, estados excluidos, datos inconsistentes, falta de lecturas, roles y restricciones de gestión, CSV/escape/precisión, conservación de datos y cancelación.

La prueba de concurrencia retiene la respuesta de una consulta real y devuelve primero otra consulta posterior. El servicio intervenido ignora la cancelación de la primera. Se envían los callbacks mediante el mecanismo de eventos de Blazor y se comprueba el HTML del componente: el resultado más reciente no cambia al llegar la respuesta anterior. No son pruebas unitarias de Presentation ni requieren paquetes nuevos.

```powershell
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --no-restore -m:1 --disable-build-servers -p:BaseOutputPath=bin/req008/ --filter 'FullyQualifiedName~Reportes' --logger trx --results-directory artifacts/req008/reportes-final
```

### Regresión funcional y arquitectura

Se configuró externamente `SOLARUY_HISTORICO_TEST_CONNECTION` con una base exclusiva `solaruy_req007_test_reportes` en MariaDB 11.8, puerto local 3307. La compilación se realizó en una salida separada para no bloquear los binarios de la aplicación habitual.

```powershell
dotnet test SolarUY.sln --no-restore -m:1 --disable-build-servers -p:BaseOutputPath=bin/req008/ --filter 'FullyQualifiedName!~Volumen' --logger trx --results-directory artifacts/req008/funcionales-final
```

| Proyecto | Correctas | Fallidas | Omitidas |
| --- | ---: | ---: | ---: |
| CodeQuality.Tests | 9 | 0 | 0 |
| SolarUY.Domain.Tests | 46 | 0 | 0 |
| SolarUY.Application.Tests | 8 | 0 | 0 |
| SolarUY.Integration.Tests, excepto volumen | 88 | 0 | 0 |
| **Total** | **151** | **0** | **0** |

Los controles arquitectónicos se ejecutaron después de los cambios, sin modificar sus archivos. No se añadieron pruebas unitarias de las capas externas, dependencias o versiones nuevas.

### Solución completa y rendimiento previamente pendiente

También se ejecutó `dotnet test SolarUY.sln` con los mismos parámetros de compilación e integración, sin excluir volumen. En ese momento, antes de agregar el caso de concurrencia, el resultado fue **150 correctas y un fallo, sin omisiones**. El fallo es el escenario existente de req7 con un millón de lecturas y cuatro consultas concurrentes:

- Parque/día: máximo 1,731 s; memoria adicional máxima de aplicación 3,87 MiB.
- Parque/mes: máximo **5,685 s**, frente al umbral de 5 s; memoria adicional máxima de aplicación 0,85 MiB.

El escenario se detiene al fallar el criterio mensual; no se declaran aprobados los escenarios posteriores. La tarea 6.8 de req7 sigue pendiente. No se cambió su consulta ni se relajó el umbral, y no se presenta la solución completa como aprobada. Evidencia: `artifacts/req008/solucion/`.

### Navegador y descarga

Se usaron Edge sin ventana, Keycloak 26.8.1 y MariaDB aislados, con usuarios ficticios para los tres roles y uno sin permiso. La aplicación de prueba utiliza HTTPS 7288 y tiene la recolección deshabilitada, sin acceder a la base habitual.

El recorrido final terminó con **44 comprobaciones correctas, cero fallos y cero excepciones JavaScript**. Los datos conocidos muestran 0,880 kWh diarios por parque, 0,580 kWh por nodo y 0,720 kWh anuales para ese nodo. Se verificaron barras/líneas, huecos, cero medido, cambio de representación, filtros, mantenimiento, validación de rangos, período vacío, CSV realmente descargado y denegación de gestión a Consulta. Se revisaron capturas de escritorio y móvil.

Evidencia y utilidades locales en `artifacts/req008/`: `browser.mjs`, `browser-results.json`, capturas, descargas, resultados TRX, preparador de datos y registros del host. La prueba de respuestas fuera de orden se acredita adicionalmente mediante la integración controlada descrita arriba.

Al cerrar la verificación se detuvieron la aplicación HTTPS 7288 y Edge de prueba, y se eliminaron únicamente los contenedores aislados creados para req8. Se comprobaron libres los puertos 7288, 9238, 3307 y 8082. No se modificaron los servicios habituales, credenciales, datos de la base local ni procesos de la aplicación en 5219.

## Uso

Recompilar y reiniciar la aplicación habitual, luego abrir `/reportes` o el nuevo enlace Reportes del menú. Se reutilizan las migraciones de req7 ya existentes; esta entrega no requiere una migración nueva. La disponibilidad de energía depende de las lecturas persistidas y de la cobertura del período seleccionado.


## Revisión consolidada de reportes

### Implementación: REQ020 — Reportes con gráficos del histórico y exportación

Fecha de cierre: 12/11/2026. **Las 47 tareas de `tasks.md` están completas.** Se reutilizó la funcionalidad existente, se corrigió una condición de carrera y se ajustó la legibilidad del resumen de mantenimiento. La regresión final terminó con **241 pruebas correctas, 0 fallidas y 4 omitidas explícitamente**. Se completó la inspección de Chrome con los tres roles y la descarga real de los CSV.

## Resultado funcional

La pantalla `/reportes` integra generación histórica en kWh mediante barras y líneas Radzen, filtros de parque/nodo/panel/período/fechas, comparación, resumen de energía y cobertura, tabla y extremos, mantenimientos realizados y CSV por período. No existe una segunda pantalla de Histórico.

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
| `prompting/req8_Reportes_Con_Graficos_Del_Historico/specs.md` | Variante complementaria de concurrencia sin MariaDB y legibilidad de columnas. |
| `prompting/req8_Reportes_Con_Graficos_Del_Historico/tasks.md` | Cierre de las tareas con evidencia. |
| `prompting/req8_Reportes_Con_Graficos_Del_Historico/implementations.md` | Informe final. |

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

Se comprobaron `git diff --check`, UTF-8 sin BOM y coherencia entre requisitos, especificación y tareas. No quedan tareas de REQ020 pendientes. Las omisiones explícitas de la suite general se conservan documentadas y no se contabilizan como pruebas aprobadas.

## Interfaz unificada — 17/11/2026

Reportes integra toda la consulta histórica y comparación. Se amplían los filtros con PanelId validado, también aplicado al mantenimiento y exportación. Se conserva la lógica de cálculo de req7 sin migraciones. La navegación tiene una sola entrada; la URL antigua es un alias. Verificación: 32 integraciones de reportes y concurrencia aprobadas, incluida consulta por panel, CSV y rechazo de pertenencia inválida; 9 pruebas de arquitectura aprobadas. El recorrido visual de esta versión queda pendiente.
