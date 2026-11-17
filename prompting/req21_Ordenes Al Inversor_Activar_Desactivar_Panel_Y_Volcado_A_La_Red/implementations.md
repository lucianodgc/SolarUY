# Implementación de req21 — Órdenes al inversor

## Acuerdos y alcance

D1–D4 están documentadas en `specs.md`. Las cuatro órdenes usan el mismo `IInversorClient` TCP existente. El volcado corresponde al inversor configurado completo, independientemente del parque seleccionado. Encender/apagar el inversor conserva su comportamiento anterior y queda fuera de las cuatro órdenes auditadas de req21.

## Cambios

- Domain: `IntentoOrdenInversor`, acciones y estados, identidad propia, actor observado, solicitud y destino históricos, UTC, códigos acotados, confirmación con autorización/ACK y resultados terminales. Pruebas simultáneas en `IntentoOrdenInversorTests`.
- Application: `OrdenInversorService`, DTO de solicitud con correlación, DTO de resultado e interfaces de servicio/repositorio. Autoriza cada llamada mediante el contexto del servidor y exige `sub` real y Administrador efectivo. Valida panel/nodo contra inventario y vuelve a comprobarlos antes de enviar; no acepta actor ni roles del navegador.
- `InversorService` delega sus cuatro mutaciones existentes al servicio auditado; conserva consultas y encendido. Una respuesta distinta de Confirmada o una auditoría incompleta no se presenta como éxito de esa ruta antigua.
- Infrastructure: `IntentoOrdenInversorRepository`, configuración EF y migración `20261117025007_AddAuditoriaOrdenesInversor`. La tabla nueva no tiene cascadas ni FK hacia inventario o usuarios: conserva identificadores y nombre observado aunque luego se elimine o traslade el destino. No reconstruye intentos anteriores.
- TCP: se exige una respuesta con `exito` booleano y resultado `true` para confirmar. Resultado falso/rechazo explícito se separan de respuestas inválidas, pérdida de conexión, timeout y cancelación después de posible envío.
- Simulador: telemetría y adquisición histórica comparten la condición de generación. Desactivar conserva 0 W tras sincronizar el mismo inventario; una falla explícita sigue siendo falla. Se mantienen seis decimales en lecturas históricas y dos en telemetría. Activar no elimina fallas, no cambia el estado administrativo ni garantiza generación.
- Presentation: `OrdenesInversor.razor` en Monitoreo, confirmaciones Radzen con destino, controles de Administrador, resultados con identidad del intento y observación operacional fechada. Las muestras vencidas se muestran sin evidencia vigente; no se deduce habilitación de la potencia o auditoría. Consume la telemetría existente, sin nuevas adquisiciones, puntos sintéticos ni métodos de escritura en el hub.
- Simulador: sus controles de panel/volcado solicitan confirmación y muestran el resultado auditado. Los botones se habilitan después de conectar el circuito Blazor y se bloquean mientras una confirmación/orden está en curso.
- Autenticación: `UsuarioActualDto.Sub` es compatible con lectores que usan `Id`; las órdenes no utilizan el fallback NameIdentifier. OIDC conserva el claim original mediante `MapInboundClaims=false`, y el servicio solo admite un sub inequívoco en identidades autenticadas.
- Pruebas: integración con MariaDB/TCP, adaptación de la composición de pruebas existentes y suite Playwright aislada con Nginx/dos procesos. Las fixtures y rutas de control de pruebas no se incorporan a la aplicación productiva.
- Puente SignalR: creación, selección y liberación devuelven el control inmediatamente a Blazor; las operaciones de transporte se completan internamente con manejo de errores. Evita responder una llamada JS pendiente al circuito anterior durante el relevo. Se actualizó la versión del módulo importado en ambas pantallas.

## Ejecución, concurrencia y recuperación

La correlación es única por `sub` (comparación binaria). La misma solicitud devuelve su resultado guardado, y cambiar su acción/destino se rechaza. Un intento explícitamente nuevo recibe otra correlación. Los intentos sin sub no se agrupan como si pertenecieran a una persona ficticia.

Cada endpoint utiliza un bloqueo de sesión `GET_LOCK` en MariaDB, separado del recolector. Ambas instancias deben usar exactamente el mismo `InversorTcp:Host` y puerto, además de la misma MariaDB. No hay transacción SQL abierta durante TCP. Si el bloqueo está ocupado, no se envía la nueva orden. Los alias distintos del mismo host deben normalizarse en el despliegue: sus nombres configurados producirían bloqueos diferentes.

Primero se guarda Pendiente de forma durable. Si falla ese guardado, no se envía. La orden se ejecuta una sola vez desde el caso de uso. Después se intenta guardar el resultado con un token independiente de la solicitud: por defecto cinco segundos en total y hasta dos intentos de persistencia, sin reenviar TCP. Un fallo del cierre devuelve el resultado remoto conocido con `AuditoriaCompleta=false` y deja el pendiente durable para recuperación.

Antes de otra orden, al recuperar el bloqueo exclusivo, los pendientes abandonados pasan a Indeterminada sin reenviarse. Repetir una correlación pendiente bajo ese bloqueo también provoca su recuperación. Con MariaDB inaccesible se impide enviar, pero no se promete guardar allí nuevos intentos durante la indisponibilidad. Identificadores que exceden los límites del contrato se rechazan sin almacenar contenido arbitrario.

El protocolo no ofrece deduplicación remota ni fencing: perder la sesión de bloqueo o morir durante TCP impide garantizar ejecución exactamente una vez. Una orden Indeterminada requiere consultar evidencia actual y confirmar cualquier intento nuevo. Una confirmación auditada no sustituye una medición posterior.

## Despliegue

1. Detener las instancias antiguas o impedir que sigan aceptando órdenes: su versión anterior no pasa por auditoría. No mezclar escritores antiguos y nuevos durante el reemplazo.
2. Respaldar la base y ejecutar la migración con un único operador, desde la raíz:

   ```powershell
   dotnet ef database update --project SolarUY.Infrastructure --startup-project SolarUY.Presentation
   ```

   Usar la conexión configurada para ese entorno; no migrar automáticamente desde ambas instancias. La migración se probó únicamente contra MariaDB temporal; no se aplicó sobre la base habitual del usuario.

3. Configurar en ambas instancias el mismo endpoint TCP, MariaDB y claves/cookies compartidas existentes. Mantener un solo simulador/inversor común. No iniciar simuladores privados por instancia.
4. Parámetros opcionales: `OrdenInversor__CierreAuditoriaSegundos=5` (1–30) y `OrdenInversor__IntentosPersistencia=2` (1–2). El arranque valida estos rangos. No se incorporaron bibliotecas ni servicios adicionales.
5. Iniciar ambas instancias y Nginx. Volver a iniciar sesión si la cookie anterior solo tenía NameIdentifier; las órdenes exigen el `sub` original. Verificar que Monitoreo y Simulador muestran el mismo estado.

La configuración Nginx utilizada por la suite de req21 es una fixture de pruebas con afinidad explícita para fijar A/B y respaldo cruzado. No reemplaza el template de despliegue existente.

## Verificación y reproducción

Resultados finales del 17/11/2026: **75 pruebas Domain, 16 Application, 9 CodeQuality y 59 de integración aprobadas**, sin omisiones en esas ejecuciones. **1 recorrido Playwright aprobado** en Chromium aislado, con dos procesos reales detrás de Nginx y endpoint TCP común (18,2 segundos de recorrido; 25,8 segundos de suite). EF confirmó que no hay cambios de modelo pendientes de migración y `git diff --check` no encontró errores. Se inspeccionó la captura completa del resultado.

| Verificación | Evidencia |
|---|---|
| V1 | Construcción, UTC, evidencia de confirmación, transiciones terminales, identidad/destino inmutables; 75 pruebas Domain. |
| V2 | 9 guardarraíles CodeQuality aprobados sin modificaciones; 16 pruebas Application existentes aprobadas. Las pruebas nuevas fuera de Domain son integraciones con DB/TCP/navegador. |
| V3 | Cuatro acciones reales por TCP y auditoría real; ACK verdadero/falso, rechazo remoto, JSON/tipo/resultado ausentes o inválidos, desconexión, timeout y cancelación posterior al envío. Repetir correlación no reenvía. |
| V4 | Administrador, Tecnico, Consulta, anónimo, actor sin sub y combinación de roles por llamada directa. Ninguna orden no autorizada cambia el flag remoto; se registra actor real/nulo. Navegador verifica ausencia de controles para Tecnico/Consulta. |
| V5 | Registro y recuperación en MariaDB; fallos inicial/final inyectados alrededor del repositorio real, cierre independiente de cancelación, resultado remoto separado de auditoría incompleta, UTC y migración incremental que conserva lecturas/inventario. Códigos acotados; sin credenciales ni payloads TCP persistidos. |
| V6 | Desactivar produce 0 W en adquisición registrada y telemetría, permanece después de sincronizar y conserva Sano; una falla explícita permanece Fallado. Activar con inversor apagado sigue dando 0 W. Navegador observa 0 W automáticamente en A/B y salud mantiene su política aprobada. |
| V7 | Activar/desactivar volcado global, consulta posterior y repetición de estado alcanzado. No se modifica el inventario como si la respuesta fuera por nodo. |
| V8 | Confirmación/cancelación, destino, doble clic, resultados e identificador, roles, cambio de parque, reconexión forzada del hub y controles existentes de Simulador. Cero errores JavaScript al finalizar. |
| V9 | A/B emiten y observan órdenes mediante TCP compartido y MariaDB. Se apaga A, la pantalla recupera el circuito y permite otra orden; luego se restaura A y se confirman órdenes opuestas simultáneas desde ambas. Se registran ocho intentos sin correlaciones duplicadas: una orden ocupada puede rechazarse sin enviar. El bloqueo/repetición/recuperación también se verifican con sesiones MariaDB independientes. |

No se ejecutaron una autenticación interactiva contra el Keycloak habitual, un despliegue sobre la base habitual, pruebas de hardware eléctrico ni una prueba prolongada de partición de red. No se presentan esas comprobaciones como aprobadas. No se ejecutó la solución completa: se seleccionaron las suites afectadas, además de Domain, Application y CodeQuality. Los escenarios de salud se verificaron mediante la suite de integración existente y el recorrido nuevo; no se repitieron sus suites históricas completas de navegador.

La base aislada de esta ejecución fue `solaruy_req007_test_req017` en el puerto 33319; no se usó la base habitual. Los contenedores temporales se llaman `solaruy-req017-test` y `solaruy-req017-nginx`; se detienen y eliminan al finalizar. Docker Desktop permanece disponible.

Para repetir las pruebas de integración, configurar `SOLARUY_HISTORICO_TEST_CONNECTION` apuntando a una base desechable cuyo nombre comience por `solaruy_req007_test`. La fixture rechaza otros nombres. Ejecutar las suites que comparten esa base en forma secuencial, y liberar 5051/5052 antes de Playwright.

```powershell
dotnet test SolarUY.Domain.Tests/SolarUY.Domain.Tests.csproj -m:1 -nodeReuse:false
dotnet test CodeQuality.Tests/CodeQuality.Tests.csproj -m:1 -nodeReuse:false
dotnet test SolarUY.Integration.Tests/SolarUY.Integration.Tests.csproj --filter "FullyQualifiedName~OrdenInversorIntegrationTests|FullyQualifiedName~InversorIntegrationTests|FullyQualifiedName~InversorTcpIntegrationTests|FullyQualifiedName~SaludPanelIntegrationTests" -m:1 -nodeReuse:false
```

Para navegador, levantar Nginx de pruebas montando `SolarUY.Browser.Tests/fixtures/ordenes.nginx.conf` en `/etc/nginx/conf.d/default.conf`, puerto local 18088 hacia 8080. En `SolarUY.Browser.Tests`, configurar `SOLARUY_MONITOREO_PROXY_URL=http://127.0.0.1:18088`, `PLAYWRIGHT_BROWSERS_PATH` hacia `.browsers` y la conexión temporal anterior; ejecutar:

```powershell
npm test -- --config=playwright.ordenes.config.mjs
```

La suite inicia y detiene dos procesos reales de Presentation, utiliza un TCP compartido, crea cookies solo dentro de la fixture de pruebas y conserva captura/traza de fallo en los directorios ignorados de Playwright. No usa el perfil personal del navegador.


## Archivos de la entrega

```text
SolarUY.Application/Exceptions/OrdenInversorRechazadaException.cs
SolarUY.Application/Inversores/ContratosOrdenInversor.cs
SolarUY.Application/Inversores/OrdenInversorService.cs
SolarUY.Browser.Tests/fixtures/ordenes.nginx.conf
SolarUY.Browser.Tests/ordenes-teardown.mjs
SolarUY.Browser.Tests/playwright.ordenes.config.mjs
SolarUY.Browser.Tests/tests/ordenes.spec.mjs
SolarUY.Domain.Tests/IntentoOrdenInversorTests.cs
SolarUY.Domain/Entities/IntentoOrdenInversor.cs
SolarUY.Infrastructure/Migrations/20261117025007_AddAuditoriaOrdenesInversor.Designer.cs
SolarUY.Infrastructure/Migrations/20261117025007_AddAuditoriaOrdenesInversor.cs
SolarUY.Infrastructure/Persistence/IntentoOrdenInversorConfiguration.cs
SolarUY.Infrastructure/Persistence/IntentoOrdenInversorRepository.cs
SolarUY.Integration.Tests/OrdenInversorIntegrationTests.cs
SolarUY.Integration.Tests/OrdenNginxIntegrationTests.cs
SolarUY.Presentation/Components/Shared/OrdenesInversor.razor
SolarUY.Presentation/Monitoreo/MensajeResultadoOrden.cs
prompting/req21_Ordenes Al Inversor_Activar_Desactivar_Panel_Y_Volcado_A_La_Red/implementations.md
SolarUY.Application/Abstractions/UsuarioActualDto.cs
SolarUY.Application/Inversores/InversorService.cs
SolarUY.Infrastructure/DependencyInjection.cs
SolarUY.Infrastructure/Inversores/InversorSimulador.cs
SolarUY.Infrastructure/Inversores/Tcp/InversorTcpClient.cs
SolarUY.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs
SolarUY.Integration.Tests/HistoricoDatabase.cs
SolarUY.Integration.Tests/InversorIntegrationTests.cs
SolarUY.Integration.Tests/InversorTcpIntegrationTests.cs
SolarUY.Integration.Tests/MonitoreoHubIntegrationTests.cs
SolarUY.Integration.Tests/MonitoreoNginxIntegrationTests.cs
SolarUY.Integration.Tests/SaludNavegadorIntegrationTests.cs
SolarUY.Integration.Tests/SaludNginxIntegrationTests.cs
SolarUY.Presentation/Authentication/UsuarioActualService.cs
SolarUY.Presentation/Components/Pages/Monitoreo.razor
SolarUY.Presentation/Components/Pages/SimuladorInversor.razor
SolarUY.Presentation/Program.cs
SolarUY.Presentation/wwwroot/js/monitoreo.js
prompting/req21_Ordenes Al Inversor_Activar_Desactivar_Panel_Y_Volcado_A_La_Red/specs.md
prompting/req21_Ordenes Al Inversor_Activar_Desactivar_Panel_Y_Volcado_A_La_Red/tasks.md
```
