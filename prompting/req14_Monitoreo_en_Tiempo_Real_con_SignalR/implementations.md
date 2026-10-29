# Implementación de req14: Monitoreo en tiempo real

Fecha: 29/10/2026, America/Montevideo.

## Estado y acuerdos

El usuario comunicó la aprobación de Luciano y autorizó completar D2 con la MariaDB compartida existente, sin Redis ni servicios adicionales. También autorizó Playwright con Chromium aislado, sin utilizar su perfil de Brave. D1/D3/D4/D5 se mantienen: intervalos UTC compatibles, vigencia de dos intervalos, totales parciales explícitos, curva de hasta 120 puntos configurables, cliente oficial SignalR 8.0.17 local y topología actual sin reasignar muestras históricas.

Se completaron la distribución durable, la configuración reproducible de Nginx para dos instancias y las verificaciones del cliente interactivo. Esto no constituye un despliegue sobre el entorno habitual: la migración y las pruebas se ejecutaron en bases y procesos temporales aislados.

## Comportamiento implementado

La pantalla /monitoreo obtiene el estado inicial desde lecturas persistidas, muestra parque/nodos/paneles en W con fechas UTC y distingue cero, ausencia, expiración, intervalo incompatible y muestra de nodo anterior. El dominio calcula las contribuciones sin duplicarlas. El gráfico Radzen conserva puntos acotados, actualiza el mismo intervalo y separa tramos después de huecos.

Cada lote se guarda junto con un aviso durable en una transacción. La fila coordinadora de SecuenciaMonitoreo serializa las secuencias hasta el commit, evitando que un consumidor avance sobre una transacción todavía no confirmada. Un rollback revierte lecturas, aviso y secuencia. Se conserva el bloqueo global del recolector de req011.

Cada instancia lee AvisosMonitoreo con su propio cursor persistido en CursoresMonitoreo. Confirma el avance únicamente después de entregar el aviso a su hub local. Ante error de MariaDB, caída del proceso o fallo del notificador, reintenta desde la posición confirmada. La entrega es al menos una vez: las repeticiones invalidan el estado, pero no crean lecturas ni duplican puntos. No se promete entrega exactamente una vez. Los avisos no se purgan automáticamente en esta versión.

El aviso inmediato del recolector permanece como optimización; la recuperación no depende de él. Infrastructure publica dentro del guardado y ofrece el contrato de acceso al registro. El worker de Presentation consume ese contrato y llama al adaptador local con IHubContext, sin referencias de Infrastructure a Presentation.

La carga inicial, la reconciliación tras suscribirse y la reconexión consultan el estado persistido. Los cambios rápidos de parque cancelan o descartan respuestas anteriores. La expiración se reevalúa con el snapshot incluso si una consulta SQL está bloqueada por una interrupción. El instante de evaluación se toma después de recibir la consulta para no presentar datos vencidos como actuales.

Nginx admite negociación HTTP y WebSocket para Blazor y el hub propio, usa cookie de afinidad y otro backend como respaldo. Comparte el estado de disponibilidad entre workers y actualiza la cookie según la instancia que realmente respondió. Ambas aplicaciones comparten claves de Data Protection y nombre SolarUY. Si el circuito Blazor original ya no existe, /monitoreo recarga automáticamente y conserva el parque en la URL; las otras pantallas muestran una instrucción de recarga para preservar formularios. El cliente evita invocar callbacks .NET sobre un circuito desconectado y libera conexiones, temporizadores y listeners al salir.

## Archivos principales

| Área | Archivos |
|---|---|
| Domain | SolarUY.Domain/Monitoreo/GeneracionActualService.cs; SolarUY.Domain.Tests/GeneracionActualTests.cs |
| Application | SolarUY.Application/Monitoreo/: contratos, DTO, MonitoreoService, SerieMonitoreoService y acceso autorizado |
| Integración de adquisición | ContratosRecoleccion.cs, RecoleccionLecturasService.cs, RecoleccionLecturasRepository.cs y RecoleccionLecturasWorker.cs |
| Infrastructure | Persistence/MonitoreoRepository.cs, DistribucionMonitoreoRepository.cs y DependencyInjection.cs |
| Migración | 20261029120000_AddDistribucionMonitoreo.cs y su Designer: solo tres tablas técnicas; no nuevas entidades del dominio solar |
| Presentation | MonitoreoHub, opciones, NotificadorLecturasConfirmadas, DistribucionMonitoreoWorker, Program.cs y appsettings.json |
| Interfaz | Monitoreo.razor, NavMenu.razor, App.razor, js/monitoreo.js, js/reconexion.js y SignalR local con licencia |
| Despliegue | deploy/nginx/monitoreo.conf.template, compose.yaml y README.md |
| Integración | MonitoreoIntegrationTests, MonitoreoHubIntegrationTests, DistribucionMonitoreoIntegrationTests y MonitoreoNginxIntegrationTests |
| Navegador | SolarUY.Browser.Tests/: configuración, cierre global, tests/monitoreo.spec.mjs, package.json y lockfile |

Los cambios previos de req011 permanecen en el árbol de trabajo; no se modificaron los guardarraíles de CodeQuality.Tests ni las reglas/habilidades del proyecto.

## Configuración y despliegue

Seguir [deploy/nginx/README.md](../../deploy/nginx/README.md). Aplicar las migraciones con un único operador antes de iniciar los binarios nuevos. Si está pendiente req011, revisar primero sus comprobaciones de datos históricos. Los recolectores antiguos no escriben avisos durables: deshabilitar su adquisición durante la transición y habilitarla solamente en versiones compatibles.

| Configuración | Uso |
|---|---|
| ConnectionStrings__SolarUY | Conexión MariaDB externa, sin secretos versionados |
| DistribucionMonitoreo__InstanciaId | Identificador único y estable por proceso, hasta 100 caracteres; solaruy-local solo sirve para ejecución única |
| DistribucionMonitoreo__IntervaloSegundos | Lectura del registro compartido, 1 s predeterminado |
| DistribucionMonitoreo__TamanoLote | Hasta 100 avisos por ciclo de forma predeterminada |
| DataProtection__RutaClaves | Directorio privado realmente compartido entre instancias |
| ReverseProxy__Direccion | Dirección concreta del proxy confiable para encabezados reenviados |
| Monitoreo__MaximoPuntosGrafico | 120 predeterminado; las pruebas lo reducen a 3 |
| Monitoreo__ReevaluacionSegundos | Reevaluación temporal independiente de nuevas lecturas |

Conservar la configuración externa de Keycloak, conexión y adquisición. El proxy de ejemplo se enlaza a loopback:18088; las aplicaciones de prueba usan 5051/5052. Integrar el listener HTTPS y certificados del despliegue de Luciano; no se instalaron certificados ni se modificó un servidor de producción. Reemplazar una instancia por vez, manteniendo la otra atendiendo.

## Verificación reproducible

Se utilizaron MariaDB 11.8 y Nginx 1.30.6 en contenedores temporales propios, dos procesos reales Kestrel y Chromium aislado con @playwright/test 1.64.1. Las cookies de los escenarios automatizados se firman exclusivamente en el fixture de pruebas con claves temporales; no hay endpoints de autenticación alternativos en la aplicación.

Para integración, definir SOLARUY_HISTORICO_TEST_CONNECTION con una base dedicada cuyo nombre comience con solaruy_req007_test, y SOLARUY_MONITOREO_PROXY_URL apuntando al Nginx configurado para los puertos de prueba. Ejecutar dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj. La base temporal final aplica todas las migraciones desde cero.

Para navegador, en SolarUY.Browser.Tests ejecutar npm ci, instalar Chromium con npx playwright install chromium y ejecutar npm test. Definir las dos variables anteriores, SOLARUY_MONITOREO_DB_CONTAINER con el nombre del contenedor de prueba propio y, opcionalmente, PLAYWRIGHT_BROWSERS_PATH. La prueba pausa y reanuda ese contenedor; nunca debe señalar una base de trabajo compartida. Playwright inicia el fixture de dos servidores, controla el relevo y verifica su cierre. Los artefactos y dependencias quedan ignorados por git.

Casos de navegador aprobados:

1. Clientes confirmados en A/B reciben avisos y actualizan el gráfico sin recargar; la curva conserva solo tres puntos en la configuración de prueba.
2. Cambios rápidos de parque mantienen la última selección frente a avisos tardíos.
3. Repetir un aviso no duplica puntos; desconectar el WebSocket recupera la suscripción; abandonar la pantalla cierra la conexión y no crea reintentos posteriores.
4. Pausar MariaDB vence las muestras sin nuevas mediciones; al reanudar se recuperan actualizaciones.
5. Detener A mantiene B disponible y recupera circuito, cookie, parque y gráfico sin errores de JavaScript.

Se inspeccionaron las capturas de página completa: jerarquía, estados, unidades, fechas UTC y gráfico visibles sin superposición. Se conservan en SolarUY.Browser.Tests/test-results durante la ejecución local.

## Resultados finales

- Domain: 51 correctas.
- Application existente: 16 correctas.
- CodeQuality: 9 correctas, sin modificar sus pruebas.
- Playwright: 5 correctas; fixture de servidores: 1 correcto y cerrado sin errores.
- Integración completa: 137 correctas, 2 fixtures interactivos omitidos, 139 casos totales; incluye dos procesos reales detrás de Nginx. La primera ejecución tuvo un único fallo de rendimiento histórico (5,04–5,15 s frente al límite de 5 s) mientras se ejecutaban otras comprobaciones. El caso pasó aislado y la repetición completa, sin compilaciones simultáneas, pasó sin errores; no se cambiaron consultas ni umbrales para obtener este resultado.
- EF Core: sin cambios pendientes del modelo respecto de la última migración.
- Nginx: nginx -t correcto.
- JavaScript: node --check correcto para monitoreo.js y reconexion.js.
- Git: git diff --check correcto.

Los dos fixtures interactivos se omiten en la ejecución general porque requieren activación explícita; el fixture de dos instancias se ejecutó y aprobó desde Playwright. El fixture manual antiguo queda sustituido por esa verificación automatizada. No se ejecutaron pruebas contra el despliegue productivo, TLS real, Keycloak externo ni el perfil personal del navegador; los cambios están preparados y verificados localmente para su despliegue coordinado.
