# Implementación de req016: Estado de salud y alertas de fallas

## Estado y autorización

Se implementaron y verificaron las decisiones D1–D6, incluido el observador distribuido y V8. El estado describe la solución del proyecto y las pruebas aisladas; no acredita un despliegue productivo.

## Reglas implementadas

- Salud nueva en `SolarUY.Domain.Salud`: Normal, Degradado, Falla y Desconectado. Se conservan los enums previos del inversor, `LecturaPanel.EstadoSalud` y `Panel.Estado`.
- Falla explícita con lectura vigente produce Falla. Al alcanzar tres intervalos sin lectura válida se produce Desconectado. Un panel sin primera lectura espera ese plazo desde su primera observación persistida; un traslado vuelve a iniciar la referencia del nodo actual.
- El dominio incorpora límites configurables 50 %/80 % y factor esperado; comprueba potencia baja solo si existe evidencia explícita de generación esperada. La integración histórica la mantiene deshabilitada y rechaza activarla hasta que el contrato aporte las condiciones de operación. Una señal sana no se convierte en falla por potencia cero.
- Evidencia desconocida, futura o de procedencia no demostrable no se convierte en Normal. Se conserva el último estado conocido con suficiencia/vigencia separada. La consulta también invalida evaluaciones antiguas si el evaluador deja de funcionar.
- Nodo: Falla > Desconectado > Degradado > Normal, con cobertura explícita; nodo vacío o sin estados suficientes muestra falta de datos.
- Primera transición con anterior nulo; solo un cambio de estado crea historial. La versión por panel permite ordenar transiciones. La fecha del cambio es la evaluación UTC y la fecha de evidencia se conserva separada.
- Una alerta activa por panel. Cambiar de condición cierra el episodio anterior y abre otro; Normal cierra; una recurrencia tiene identidad nueva. La insuficiencia no cierra automáticamente la última condición confirmada.

## Persistencia e integración

`SaludPanelRepository` bloquea en orden las filas de los paneles de un lote de hasta 100, incluso antes de que exista su primera fila de salud. Consulta evidencia en conjunto y confirma estado, transiciones y alertas en una transacción con aislamiento ReadCommitted. Los lectores utilizan una instantánea RepeatableRead. No se mantiene la transacción durante una consulta al inversor ni un envío de SignalR.

La migración incremental `20261112035214_AddEstadoSaludAlertas` agrega SaludPaneles, CambiosEstadoSaludPanel y AlertasPanel. La unicidad de alerta activa se impone mediante columna calculada nullable e índice único compatible con MariaDB; RTRIM hace la expresión independiente del relleno de CHAR. Los episodios cerrados tienen clave activa nula y pueden conservarse sin impedir recurrencias. Las relaciones son Restrict para evitar borrado de historial por cascada. Por eso una eliminación de un panel con historial de salud requiere una política posterior y no se amplió en este requerimiento.

El worker de salud evalúa al arrancar y cada cinco segundos. La adquisición también solicita evaluación después del commit. Si falla salud, la lectura permanece y el próximo ciclo recupera el trabajo; no se llama nuevamente al inversor. Si falla el aviso local, el próximo ciclo confirma una nueva invalidación sin crear otra transición ni adquisición.

Cada ciclo confirmado invalida los parques locales evaluados, incluso si no cambió el estado: permite renovar la vigencia y recuperar un aviso fallido. La entrega inmediata local se complementa ahora con ObservacionSaludWorker en cada instancia. No se afirma entrega exactamente una vez y no se restauraron las tablas retiradas de req014.

## Interfaz y seguridad

`SaludMonitoreo` se integra en la página Monitoreo. Incluye indicadores con texto por panel y nodo, motivos, evidencia UTC, lista Radzen de alertas activas y acceso al historial con paginación en servidor. Las alertas identifican nodo/parque actuales. Los estados de negocio se calculan fuera de Razor.

El evento local SaludActualizada usa el hub autorizado existente. La carga inicial, la resuscripción y la reconexión consultan el estado confirmado. El componente descarta resultados de parques anteriores, gestiona invalidaciones concurrentes y libera sus recursos. Un temporizador de vista solo reevalúa la vigencia del snapshot, sin adquirir lecturas ni consultar SQL. La actualización de salud no modifica los puntos del gráfico de potencia.

Application valida autenticación y los roles Administrador, Tecnico y Consulta antes de consultar datos; comprueba pertenencia panel/parque y límites de paginación. El hub conserva sus restricciones. No se agregaron comandos de edición/cierre manual a Presentation. Los endpoints de simulación/autenticación de las pruebas existen únicamente dentro del host de Integration.Tests.

## Archivos principales

| Área | Archivos |
|---|---|
| Domain | Salud/EstadoSaludPanel.cs, PoliticaSalud.cs, EvaluacionSaludService.cs, SaludPanel.cs |
| Application | Salud/ContratosSalud.cs, SaludPanelService.cs, ConsultaSaludService.cs |
| Infrastructure | Persistence/SaludPanelConfiguration.cs, SaludPanelRepository.cs, ObservacionSaludRepository.cs, DependencyInjection.cs, migración AddEstadoSaludAlertas y snapshot |
| Presentation | Monitoreo/SaludPanelWorker.cs, Monitoreo/ObservacionSaludWorker.cs, Program.cs, BackgroundServices/RecoleccionLecturasWorker.cs, appsettings.json |
| Interfaz | Components/Shared/SaludMonitoreo.razor y CSS, Components/Pages/Monitoreo.razor, wwwroot/js/monitoreo.js |
| Pruebas | Domain.Tests/SaludPanelTests.cs; Integration.Tests/SaludPanelIntegrationTests.cs, SaludNavegadorIntegrationTests.cs; ajustes de limpieza de HistoricoDatabase y servicios del host MonitoreoHubIntegrationTests |
| Navegador | Browser.Tests/playwright.salud.config.mjs, salud-teardown.mjs y tests/salud.spec.mjs |

## Configuración y ejecución

La sección SaludPanel agrega IntervalosDesconexion=3, EvaluacionSegundos=5, UmbralFalla=0.5, UmbralDegradado=0.8, FactorEsperado=1 y EvaluarPotenciaBaja=false. Los rangos se validan al arrancar. Las dos instancias deben compartir la misma política. SaludPanel:Habilitada=false desactiva evaluación periódica y posterior al commit, permitiendo una instancia solo observadora. SaludDistribucion agrega Habilitada=true, InstanciaId=solaruy-local e IntervaloSegundos=1; usar identificadores distintos por proceso. El período del observador no puede superar el de evaluación. Instancia:Id conserva compatibilidad con el encabezado de afinidad y debe coincidir con el ID del proxy. No se agregaron dependencias ni servicios externos.

Para desplegar localmente, con la conexión externa configurada y el evaluador detenido, aplicar `dotnet ef database update --project SolarUY.Infrastructure` desde la raíz antes de ejecutar Presentation. La migración de salud se probó exclusivamente sobre bases aisladas; no se aplicó a la base habitual del usuario. No es necesario volver a levantar Docker si ya está disponible.

Pruebas de integración: definir SOLARUY_HISTORICO_TEST_CONNECTION con una base aislada cuyo nombre comience con solaruy_req007_test y ejecutar el proyecto SolarUY.Integration.Tests. Para aislar los escenarios nuevos, filtrar FullyQualifiedName~SaludPanelIntegrationTests. El fixture rechaza nombres de base habituales antes de limpiar datos.

Prueba de navegador: desde SolarUY.Browser.Tests, con la misma variable de conexión a una base aislada y PLAYWRIGHT_BROWSERS_PATH apuntando al Chromium de prueba, ejecutar `npm test -- --config=playwright.salud.config.mjs`. Inicia un host temporal en 127.0.0.1:5069, usa el simulador existente y el circuito real de adquisición, comprueba el resultado y lo cierra. No utiliza perfiles personales ni cambia el servidor habitual.

## Resultados y límites

- Domain: 69 pruebas correctas, incluidas las nuevas reglas.
- CodeQuality: 9 correctas, sin modificar guardarraíles.
- Integración nueva: 13 correctas, incluyendo reintento de aviso local y traslado de panel.
- Integración y regresión seleccionadas: 57 correctas en la ejecución conjunta final: los 49 casos previos relacionados, 6 de observación compartida y 2 variantes con procesos reales detrás de Nginx.
- Interfaz en Chromium aislado: 1 prueba funcional completa correcta, y su fixture de servidor terminó correctamente. Verificó falla/recuperación, historial, desconexión, cambio de parque, reconexión y liberación de suscripciones, sin errores de JavaScript ni recarga. Latencia observada desde solicitud de falla hasta indicador: 975 ms en la última ejecución, con adquisición/evaluación de prueba de 1 segundo. No es una garantía para la configuración habitual de 30/5 segundos.
- Modelo EF: sin cambios pendientes respecto de la migración. JavaScript y git diff --check correctos.

Se inspeccionó la captura final: indicadores de nodo/panel coloreados, motivos completos y UTC legible sin recorte. La suite general de integración no se ejecutó completa: se seleccionaron los 57 casos relacionados con salud y sus integraciones.

La configuración del proyecto deja A/B como candidatas a recolectar con exclusión MariaDB, evaluación y observación activas e identificadores distintos; B no depende de la salud de A. No se ejecutó un despliegue productivo, TLS real ni un reemplazo de los servicios habituales.

## D6: observación compartida y alta disponibilidad verificadas

El contrato IObservacionSaludRepository y SnapshotSaludDto obtiene parques y firmas por panel en dos consultas con snapshot coherente. ObservacionSaludWorker libera el contexto antes del envío al adaptador local, compara firmas por panel y retiene la instantánea confirmada solo después del envío completo. Se invalida el parque anterior y actual de un traslado; al reiniciar se avisan también parques vacíos. Se retiran entradas que desaparecieron. El procesamiento es independiente de la sesión de usuario y no adquiere mediciones.

La semántica es convergencia al estado vigente, con invalidaciones repetibles y reconstrucción al reconectar; cada transición se consulta en el historial. No hay cursor global que salte commits ni consumo del progreso de otra instancia. La cancelación del transporte sin cancelación del host se trata como fallo reintentable.

ObservacionSaludIntegrationTests pasó 6 casos reales: fallo/cancelación de envío, independencia de observadores, commit sin aviso, reinicio, commit durante lectura, traslados/retiradas y parque que quedó vacío. La medición de inventario ampliado dio 12,16 ms promedio por snapshot de 1004 paneles en cinco lecturas locales, sin SLA de rendimiento.

SaludNginxIntegrationTests pasó las dos variantes de fuente única A/B: la instancia receptora tiene adquisición y evaluación deshabilitadas y mantiene únicamente el observador. Se verificaron WebSockets, salud, cierre de alerta, unicidad de datos/transiciones/alertas, cookie compartida, cambio de afinidad y reemplazo conservando otra instancia disponible. El simulador TCP usa las capacidades de falla existentes y ambos procesos ejecutan Presentation real. Nginx -t pasó con la plantilla del proyecto.

El caso playwright.salud-distribuida.config.mjs pasó con dos contextos de Chromium aislados: encabezados prueban A/B diferentes, ambos reciben falla y recuperación sin recargar; tras detener A, su circuito se reconstruye en B conservando el parque, mientras B permanece conectado. Al restaurar A, vuelven a recibirse cambios y cierres. No hubo errores de JavaScript. La última latencia observada fue 1108 ms con adquisición, evaluación y observación de prueba a 1 segundo; no representa una garantía para otros intervalos. El fixture terminó correctamente y las capturas quedan en Browser.Tests/test-results durante la ejecución local.

Para repetir, configurar el Nginx de deploy/nginx para host.docker.internal:5051/5052 e IDs req16-a/req16-b; definir SOLARUY_MONITOREO_PROXY_URL y SOLARUY_HISTORICO_TEST_CONNECTION hacia una base de prueba solaruy_req007_test.... Ejecutar dotnet test filtrando SaludCompartida_Llega para el caso nativo. Desde SolarUY.Browser.Tests ejecutar npm test -- --config=playwright.salud-distribuida.config.mjs para navegador. Ejecutar estos escenarios secuencialmente: ambos reservan puertos 5051/5052. La configuración local de un solo host sigue en playwright.salud.config.mjs.

La guía deploy/nginx/README.md se actualizó para SaludDistribucion, claves y política compartidas, compatibilidad y reemplazo de una instancia por vez. D6 no requiere nueva migración; se necesita el esquema AddEstadoSaludAlertas ya probado. Se aplicó el procedimiento sobre las bases aisladas y los procesos temporales, sin cambiar la base habitual.


## Cierre delegado de coordinación

Se actualizó `compose.yaml`: adquisición, evaluación y observación activas en A/B; IDs de observadores distintos; B depende de MariaDB y Keycloak, sin depender de la salud de A. La exclusión por sesión y la consulta de paneles pendientes por intervalo UTC mantienen un único propietario de adquisición; la candidata restante puede continuar sin promoción manual. No hay primaria fija, garantía de relevo instantáneo ante partición ni reconstrucción automática de intervalos perdidos.

Se actualizaron `specs.md`, `tasks.md` y `deploy/nginx/README.md`. Quedan cerradas las tareas técnicas 1.7.2, 1.7 y 1. El despliegue del entorno real no forma parte de la evidencia registrada.

Verificación de este cambio: `docker compose config --quiet` y comprobación de configuración efectiva correctas (sin imprimir secretos); 9 CodeQuality aprobadas; 4 integraciones aprobadas con MariaDB temporal y TCP compartido: `DosProcesos_RecolectanSinDuplicarYContinuanTrasTerminarElPrimero`, `ConexionPerdida_NoPermiteGuardarYOtraSesionPuedeRetomar`, `DosRecolectores_SoloElPropietarioDelBloqueoLeeYGuarda` y `Workers_DosHostsRecolectanPeriodicamenteSinDuplicarYSeDetienen`. Las cuatro tardaron 6 segundos, sin omisiones. Se usaron los binarios existentes de Integration/CodeQuality (`--no-build`); no cambió C#. El simulador separado se compiló sin errores ni advertencias. La primera pasada del caso de procesos falló por falta del endpoint TCP de prueba; tras levantar el simulador real aislado, la repetición completa pasó sin modificar los tests. No se repitieron navegador ni todas las suites de la solución por este ajuste de configuración.

Entorno temporal: MariaDB `solaruy_req007_test_relevo` en 33329, contenedor `solaruy-req016-relevo` y simulador TCP en 15017. Se detienen y eliminan al finalizar. No se recrearon las aplicaciones habituales ni se migró su base. `git diff --check` sin errores.

## Corrección de borrado de inventario

Se corrigió el borrado de parques con inversores: las dependencias se eliminan en orden y la baja final no provoca conflictos con entidades ya seguidas por EF. El borrado físico de parques o nodos que tengan evaluación, cambios o alertas de salud se rechaza antes de eliminar dependencias, con un mensaje que indica usar la baja lógica de paneles.

Tres integraciones contra MariaDB aislada verifican el rechazo sin pérdida de datos para parque/nodo y el borrado correcto con inversor cuando no existe historial de salud. No se migró ni restableció la base habitual. Evidencia: `artifacts/correcciones/paneles-final/`.
