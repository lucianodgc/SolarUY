# Coordinación de req016 — distribución y recuperación de salud

Cierre de coordinación registrado el 17/11/2026 (America/Montevideo). Responsable: Luciano, según lo comunicado por el usuario.

**Estado: coordinación cerrada por validación y delegación comunicadas por el usuario.** El usuario informó que Luciano validó el mecanismo y delegó la decisión técnica al agente. Se registra esa comunicación y la decisión resultante; no se inventa la fecha original de la aprobación, un contacto directo con Luciano ni un despliegue realizado.

## Acuerdo definitivo dentro de la delegación

Conservar MariaDB como fuente de salud, alertas e historial y ejecutar `ObservacionSaludWorker` en cada instancia. El observador consulta cambios confirmados, libera la transacción antes de avisar y publica únicamente a los grupos locales SignalR autorizados del parque. No agrega Redis, otro servicio ni nuevas tablas de transporte.

Cada instancia mantiene su seguimiento en memoria. Solo lo adelanta después del envío local completo; ante error lo reintenta. Al reiniciar invalida los parques actuales, incluidos los vacíos, y los clientes reconstruyen salud, alertas e historial desde MariaDB. Los traslados invalidan el parque anterior y el actual.

La garantía es convergencia al estado vigente con historial persistente de transiciones. Los avisos pueden repetirse; no se garantiza reproducir individualmente cada invalidación intermedia ni entrega exactamente una vez al navegador. La afinidad del proxy no distribuye por sí sola los avisos de salud.

No habilitar clasificación por potencia baja hasta contar con las condiciones de operación acordadas en D1. Mantener la misma política de salud y versión de aplicación en las dos instancias.

## Configuración acordada y comprobaciones del operador

| Elemento | Base del proyecto y condición de aceptación |
|---|---|
| Aplicaciones | `solaruy-a` y `solaruy-b`, definidas en el `compose.yaml` raíz. Confirmar nombres/direcciones reales y versión a desplegar. |
| MariaDB | Ambas aplicaciones deben consultar la misma base. Confirmar conectividad, permisos y respaldo sin copiar credenciales a este documento. |
| Esquema de salud | Migración `20261112035214_AddEstadoSaludAlertas`, aplicada una sola vez por el operador. D6 no agrega otra migración. |
| Observadores | `SaludDistribucion__Habilitada=true` en A/B, identificadores distintos y estables, período de observación menor o igual al de evaluación. Confirmar la configuración efectiva; no basta revisar valores predeterminados. |
| Evaluación | Mantener al menos un evaluador habilitado; usar la misma política en ambos. La persistencia coordina evaluaciones concurrentes. |
| Adquisición | A y B habilitadas como candidatas. MariaDB concede la exclusión a una sola sesión; el propietario consulta y guarda únicamente paneles pendientes del intervalo UTC compartido. Si cae, la otra puede adquirir el bloqueo en un ciclo posterior, sin promoción manual. B depende de MariaDB/Keycloak y no de la salud de A. Al restaurarse A no hay una primaria fija. |
| Inversor | Un endpoint TCP compartido, accesible desde ambas aplicaciones. Confirmar la dirección real; no crear simuladores privados por instancia. |
| Sesiones | Claves Data Protection realmente compartidas y privadas, mismo nombre de aplicación `SolarUY` y autenticación Keycloak coherente. |
| Nginx | El template vigente usa `ip_hash`, zona compartida y proxy WebSocket. No depende de una cookie personalizada de afinidad. Confirmar resolución de backends y observar `X-SolarUY-Instancia` para comprobar qué instancia responde. |
| HTTPS y proxy confiable | Confirmar listener/certificado del entorno, URI pública de Keycloak y `ReverseProxy__Direccion` real. No aceptar cabeceras reenviadas de cualquier cliente. |

`ip_hash` puede enviar varios usuarios detrás de una misma IP a la misma instancia. Para acreditar clientes en A y B, comprobar el encabezado real de cada conexión y usar orígenes distintos o una configuración de pruebas controlada. No dar por hecho que dos pestañas del mismo equipo quedaron en instancias diferentes. La fixture explícita de afinidad de req017 es solo de pruebas y no sustituye el template vigente.

## Evidencia técnica disponible

La evidencia histórica de req016 está en [implementations.md](implementations.md): persistencia transaccional, concurrencia, observación, reintentos, reinicio, traslados y pruebas con procesos/navegador. Esa evidencia acredita el entorno aislado descrito allí, no la aprobación ni el estado del entorno real actual.

Como comprobación posterior de compatibilidad, req017 registró el 17/11/2026 75 pruebas Domain, 16 Application, 9 CodeQuality y 59 de integración aprobadas, además de un recorrido de Chromium con dos procesos, Nginx y TCP común. Ese recorrido verifica órdenes, observación, reconexión y relevo; no sustituye la comprobación específica de salud en el despliegue real. Ver [la entrega de req017](../req017_Órdenes%20al%20inversor_activar_desactivar_panel_y_volcado_a_la_red/implementations.md).

Los servicios habituales y sus certificados no se modificaron. El archivo Compose sí quedó actualizado para aplicar la elección en el próximo despliegue. El 17/11/2026 se validó su configuración efectiva y se ejecutaron cuatro integraciones de adquisición/exclusión/relevo sobre MariaDB temporal y simulador TCP compartido, además de las nueve pruebas CodeQuality: todas pasaron. La primera ejecución del caso de procesos falló por no tener el endpoint TCP de prueba levantado; se inició el simulador real del proyecto en un puerto aislado y se repitieron los cuatro escenarios correctamente, sin cambiar las pruebas.

## Secuencia acordable de despliegue y comprobación

1. Registrar responsable, entorno, versión y ventana de cambio. Respaldar MariaDB y verificar el acceso a las claves compartidas. Identificar quién conserva adquisición y evaluación durante el reemplazo.
2. Confirmar el esquema con un único operador. Desde la raíz del proyecto, con la conexión del entorno configurada externamente:

   ```powershell
   dotnet ef migrations list --project SolarUY.Infrastructure --startup-project SolarUY.Presentation
   ```

   Si faltan migraciones aprobadas, revisarlas antes de ejecutar `database update`. Ese comando aplica todas las pendientes de la versión actual, incluida req017 si está presente; no tratarlo como una actualización exclusiva de salud. No ejecutar migraciones simultáneamente desde ambos procesos.
3. Mantener B disponible y reemplazar A. Conservar compatibilidad de esquema/binarios. No habilitar workers nuevos contra tablas inexistentes ni mezclar escritores antiguos de `AvisosMonitoreo` con el esquema posterior a su retirada.
4. Verificar `/health`, identidad de instancia, inicio de sesión, WebSockets y carga del parque. Confirmar adquisición periódica y al menos un evaluador/observador operativo.
5. Con clientes cuya instancia se haya comprobado, provocar una falla y recuperación mediante el simulador del entorno de prueba acordado. A/B deben mostrar salud y alertas compatibles; el historial no debe duplicar transiciones ni alertas activas. Registrar tiempos e intervalos efectivos, sin exigir la latencia de la máquina de desarrollo.
6. Retirar A temporalmente. El cliente afectado debe reconectar o reconstruir su circuito conservando el parque; el cliente de B debe continuar. Verificar además que siguen llegando lecturas nuevas: mostrar una página disponible no demuestra continuidad de adquisición. Aplicar el relevo de recolección/evaluación acordado arriba.
7. Restablecer A y comprobar reconciliación, parques vacíos y ausencia de duplicación. Solo después, reemplazar B. Si falla una comprobación, detener el reemplazo y mantener la instancia sana.
8. Ante un problema, recuperar el binario compatible y la configuración anterior de la instancia afectada. No ejecutar automáticamente `database update` hacia atrás ni borrar tablas/historial de salud; una reversión de datos requiere un plan específico y el respaldo verificado.

## Registro de la coordinación y decisión delegada

| Dato | Registro |
|---|---|
| Responsable | Luciano, según la comunicación del usuario. |
| Evidencia | El usuario indicó «Ya lo validó; indicaré fecha y condiciones» y posteriormente «me dijo luciano que lo decidas tu de la mejor forma que se te ocurra». |
| Fecha del registro | 17/11/2026, America/Montevideo. La fecha original de la validación de Luciano no fue informada. |
| Alcance | Configuración y procedimiento del proyecto SolarUY actual; no acredita despliegue en un servidor externo. |
| Decisión delegada | Mantener observadores de salud en A/B y habilitar ambas como candidatas de adquisición con exclusión MariaDB existente. Solo una adquiere por ciclo, sin una primaria fija ni promoción manual. |
| Evaluación | Ambas habilitadas, misma política y serialización transaccional existente. |
| Identidad | Instancia y observador con identificadores distintos: solaruy-a / solaruy-b. |
| Dependencias de B | MariaDB sana y Keycloak iniciado; no depende de que A esté sana. Debe disponer de la imagen compartida de la versión. |
| Garantías | Convergencia al estado vigente, historial persistido, avisos repetibles y unicidad por panel/instante. Sin promesa de entrega exactamente una vez al navegador. |
| Condiciones operativas | Misma MariaDB, endpoint TCP, intervalo y política; claves compartidas privadas; migraciones por un único operador y reemplazo de una instancia por vez. |
| Fuera de este cierre | Certificados, direcciones/credenciales del entorno real y despliegue efectivo: los verifica el operador antes de aplicarlo. |

## Por qué se elige este relevo

Ya existe exclusión por sesión MariaDB y consulta de pendientes por intervalo UTC antes de medir. Dos candidatas no equivalen a dos adquisiciones concurrentes: quien no obtiene el bloqueo no consulta el inversor ni guarda. Si una candidata completa el intervalo y la otra obtiene luego el bloqueo, no vuelve a medir los paneles ya guardados.

El cierre de la conexión física libera la exclusión. En una caída de proceso, la candidata restante puede continuar en ciclos posteriores. En una partición de red, depende de cuándo MariaDB detecte la pérdida de sesión; no se promete relevo instantáneo. Los intervalos sin una adquisición confirmada pueden quedar sin datos y no se reconstruyen automáticamente. Si MariaDB o el endpoint común no están disponibles, esta configuración no puede mantener la adquisición.

Se conserva la tecnología implementada y se evita una promoción manual propensa a dejar B sin recolectar. No se agrega otro coordinador ni otro transporte. La consulta periódica de telemetría existente se mantiene separada de la adquisición histórica exclusiva.

## Cierre de 1.7.2

Con la validación y delegación comunicadas por el usuario, la elección técnica documentada y el ajuste de Compose, quedan cerradas 1.7.2, 1.7 y 1. No se enviaron mensajes a terceros ni se reiniciaron los servicios habituales. Las pruebas nuevas de esta configuración y sus resultados se registran en implementations.md; no confundirlas con un despliegue real.
