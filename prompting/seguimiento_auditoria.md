# Seguimiento de hallazgos H-05, H-06, H-07, H-09 y H-10

Fecha: 22/10/2026. Continuación de [la auditoría integral](auditoria_integral.md). El informe y el inventario originales se conservan como evidencia histórica; el campo `followup` de [resultados](auditoria_integral_resultados.json) contiene el estado actual.

**Resultado:** se corrigieron y comprobaron H-06, H-07 y H-10. H-05 y H-09 tienen instrucciones verificables, pero siguen dependiendo de decisiones/asignaciones y verificaciones de los responsables de cada entorno. No se marcan como completamente cerrados.

## Tabla de resolución

| Hallazgo / tipo | Causa y requisito original | Archivos modificados | Corrección realizada | Pruebas ejecutadas | Estado final |
| --- | --- | --- | --- | --- | --- |
| H-05 / datos y operación | El volcado conserva responsables de ejemplo o de otro realm. REQ004 exige que ResponsableId sea el sub de un técnico habilitado y que las tareas terminales sean inmutables. | `solaruy.sql` —solo comentario— y `keycloak/TECNICOS.md`. | Aviso sobre el carácter histórico del volcado y procedimiento administrativo para revisar/reasignar tareas abiertas. Sin cambiar filas, fijar IDs de Keycloak o modificar historiales. | Importación temporal consultable; reproducción del técnico sin tareas propias en el volcado. Recorrido específico de 12 aserciones: responsable de ejemplo → selección de técnico real → guardar → recargar → tarea visible únicamente para el asignado. Dos consultas EF posteriores confirman asignación y conservación de una tarea completada. | **PARCIAL**: procedimiento VERIFICADO; asignaciones reales pendientes del Administrador/equipo. |
| H-06 / código | El servicio de parques dependía exclusivamente de la autorización de las páginas. REQ001/006 restringen la gestión al Administrador. | `ParqueSolarService.cs`, nueva `ParqueSolarComponentBase.cs`, las tres páginas de parques, preparación de `ParqueSolarServiceTests.cs` y especificaciones REQ001/006/informe REQ006. Rutas completas abajo. | Verificación de IUsuarioActual en las seis operaciones; identidad autenticada, ID válido y rol Administrador. Se conserva la interfaz de operaciones y el ámbito de datos por componente. | 59 pruebas existentes; cinco comprobaciones de autorización, con 24 rechazos de operaciones y cambio de identidad dentro del mismo servicio; ABM/duplicados desde navegador y GET/POST denegados para Técnico/Consulta; regresión de persistencia/cascada. | **VERIFICADO**. |
| H-07 / pruebas permanentes | REQ005 pide tests de consulta, potencia diaria, activación/desactivación, volcado y fallas. La evidencia anterior estaba ignorada por Git. | Nuevo proyecto `SolarUY.Integration.Tests` —csproj, InversorIntegrationTests.cs y README—; `SolarUY.sln`, README raíz y specs/tasks/implementations de REQ005. | 19 casos xUnit permanentes de integración con composición real Application/Infrastructure, reloj e identidad controlados. Incluidos en la solución. Sin pruebas unitarias nuevas de Infrastructure ni bibliotecas de producto nuevas. | 19/19 del nuevo proyecto y 78/78 de la solución. La suite no necesita Docker, datos privados, MariaDB o Keycloak. | **VERIFICADO**. REQ005 tareas 1.6 y 3.5 cerradas con la entrega ejecutada. |
| H-09 / operación | REQ003 pide que todos los compañeros puedan arrancar la base; REQ002 deja la configuración de redirecciones públicas pendiente de definir el despliegue. | `keycloak/README.md`. | Instrucciones para distribución externa de secretos, registro de arranques reales por integrante y preparación/verificación de callbacks de producción cuando exista URL. No se inventa una URL ni se modifica el realm compartido. | Revisión de los pasos contra Compose, configuración existente y documentación de migraciones. No se afirma ejecución por compañeros ni despliegue público. | **PARCIAL / NO COMPROBADO** en los entornos ajenos. REQ003 1.3 y REQ002 5.3 siguen abiertas. |
| H-10 / documentación | Texto UTF-8 que ya contenía secuencias mal decodificadas en REQ002 y documentación de Keycloak. | `keycloak/README.md`, `prompting/req002_login_keycloak/requirements.md` y `specs.md`. | Recuperación de tildes y caracteres sin cambiar los criterios, roles, identificadores o decisiones funcionales. Guardado UTF-8 sin BOM. La solución editada al añadir tests también se guarda sin BOM. | Lectura de los documentos corregidos, decodificación UTF-8 estricta, ausencia de BOM y búsqueda de patrones de texto mal decodificado. | **VERIFICADO** para los documentos afectados. |

## H-06: coherencia con contratos y ciclo de vida

No se añadió una API ni otra capa. `IParqueSolarService` conserva sus métodos y firmas; Domain, repositorios, DbContext, migraciones y transacciones no cambian.

El constructor de `ParqueSolarService` recibe ahora `IUsuarioActual`, la misma abstracción que ya usan mantenimiento y simulador. **Es un cambio de constructor necesario**, no un cambio de la interfaz de operaciones. No se dejó un constructor alternativo que eluda la autorización. Las ocho pruebas existentes de Application conservan sus aserciones; únicamente su preparación suministra un Administrador de prueba.

Las seis operaciones autorizan antes de validar entradas o acceder al almacenamiento. No se cachea el permiso: una comprobación real cambia de Administrador a Consulta dentro de la misma instancia y la siguiente consulta se rechaza.

Las páginas usaban `OwningComponentBase<IParqueSolarService>` para disponer de un contexto independiente por componente. Resolver IUsuarioActual en ese ámbito sin inicializar su AuthenticationStateProvider puede romper la identidad del circuito. Por eso se incorpora una base compartida pequeña en Presentation:

- Conserva el ámbito propietario y su disposición.
- Recibe el proveedor del circuito e inicializa con su estado el proveedor del ámbito hijo.
- Propaga cambios de autenticación y retira la suscripción al disponer el componente.
- No crea identidades ni asigna roles y no accede a Infrastructure.
- Las rutas conservan `[Authorize(Roles = "Administrador")]`; el servicio comprueba nuevamente los permisos.

El recorrido web comprobó listado, búsqueda, alta, carga de edición, modificación y baja con este ámbito. Se mantuvieron los controles de duplicidad, formulario conservado tras error y confirmación/cancelación de baja. La propagación de eventos está implementada y revisada; no se simuló una revocación de roles en Keycloak durante un circuito abierto. La prueba de cambio de identidad se ejecutó directamente sobre el servicio.

## H-07: qué entrega la suite

`SolarUY.Integration.Tests/InversorIntegrationTests.cs` resuelve `IInversorService` y `IInversorClient` a través de DI, usando `InversorService`, el registro real `AddSolarInfrastructure` y el adaptador real. Cada prueba tiene su contenedor; el caso de estado compartido usa varios ámbitos del mismo contenedor.

Las únicas fronteras controladas son tiempo e identidad. La configuración se construye en memoria. No se lee .env, user-secrets o appsettings ni se abre una conexión a MariaDB/Keycloak.

Los 19 casos, contando los datos de las teorías de xUnit, cubren:

- Topología predeterminada/configurable, consulta y suma de potencia.
- Noche, límites solares, mañana, mediodía y tarde; hora local y zona horaria.
- Activación/desactivación, cero generación al desactivar y recuperación al activar.
- Idempotencia y aislamiento entre paneles y nodos.
- Fallas configurables, salud conservada y cero potencia aun al reactivar un panel fallado.
- Volcado activo/inactivo y estado compartido entre ámbitos; estado inicial de un contenedor nuevo.
- Consulta de Técnico/Consulta y rechazo de sus órdenes desde el servicio.
- Identidad anónima, ID vacío, rol desconocido, nodo/panel inexistente y cancelación.

Se reutilizan las versiones de xUnit 2.5.3, runner 2.5.3 y Microsoft.NET.Test.Sdk 17.8.0 que ya usa Domain.Tests. No se actualizan paquetes ni cambia la implementación del simulador. No se crean pruebas unitarias de Infrastructure. La incorporación permanente de integración cumple el pedido explícito del usuario y permite cerrar el pendiente anterior sin modificar las reglas protegidas.

Ejecución habitual:

```powershell
dotnet test SolarUY.sln
```

## Verificaciones adicionales de esta continuación

Se reutilizaron el ejecutable y el script de navegador de la auditoría. Solo se añadieron casos específicos de parques y del procedimiento H-05. Las comprobaciones visuales anteriores siguen siendo evidencia histórica: no se repitieron las cuarenta combinaciones de pantallas/ancho porque no hubo cambios de estilos.

| Grupo | Aprobadas | Fallidas / omitidas |
| --- | ---: | --- |
| Domain existente | 42 | 0 / 0 |
| Application existente | 8 | 0 / 0 |
| CodeQuality, sin modificar | 9 | 0 / 0 |
| Integración permanente del simulador | 19 | 0 / 0 |
| **Total de la solución** | **78** | **0 / 0** |
| Integración externa de regresión, reutilizada | 35 | 0 |
| Integración adicional de autorización de parques | 5 | 0 |
| Comprobaciones de importación/diagnóstico de responsables | 2 | 0; una confirma el pendiente de datos, no lo resuelve |
| Lectura EF posterior al recorrido H-05 | 2 | 0 |
| Aserciones de navegador: Administrador / roles / H-05 | 22 + 28 + 12 = **62** | 0 en los recorridos finales |

Las 24 denegaciones de parques están agrupadas en cuatro casos: Técnico, Consulta, anónimo y Administrador con ID vacío, cada uno con las seis operaciones. El quinto comprueba autorización después de un cambio de identidad.

Comando completo utilizado para compilar y probar con salida separada:

```powershell
dotnet test SolarUY.sln --no-restore -m:1 -p:BuildInParallel=false -p:BaseOutputPath=bin/PendientesVerification/
```

La repetición final secuencial de los ensamblados actuales también pasó:

```powershell
dotnet test SolarUY.sln --no-build --no-restore -m:1 -p:BuildInParallel=false -p:BaseOutputPath=bin/PendientesVerification/
```

La primera restauración del proyecto nuevo informó NU1900 por acceso a NuGet dentro del sandbox; se repitió con acceso de red y terminó correctamente. Una repetición paralela de tests quedó en espera, se interrumpió y se cerraron sus nodos de MSBuild; la repetición secuencial obtuvo el resultado anterior. Un intento del recorrido de navegador necesitó esperar la carga asíncrona de opciones de Keycloak; al corregir esa espera el recorrido terminó sin errores JavaScript. Ninguno de esos incidentes requirió alterar aserciones o lógica del producto.

Entorno externo aislado: MariaDB 11.8 en 3310, Keycloak 26.8.1 en 8180 y compilación temporal HTTPS 7271. Solo bases `solaruy_audit*`, protegidas por el guardarraíl del ejecutable. No se modificaron datos ni usuarios reales, user-secrets, .env, Compose o realm compartido.

Se cerraron el servidor y el navegador temporales y se retiraron los dos contenedores con sus volúmenes de prueba. Los contenedores originales de MariaDB, phpMyAdmin y Keycloak continúan activos. Las capturas se conservan.

Las capturas nuevas del procedimiento están en [Administrador](../artifacts/auditoria-integral/captures/seguimiento-h05-administrador.png) y [Técnico](../artifacts/auditoria-integral/captures/seguimiento-h05-tecnico.png). Los artefactos de navegador y persistencia siguen ignorados; **la suite del simulador sí se incorpora a los archivos del repositorio y la solución**.

## Archivos de implementación de H-06

- `SolarUY.Application/ParquesSolares/ParqueSolarService.cs`.
- `SolarUY.Presentation/Components/Shared/ParqueSolarComponentBase.cs`, nuevo.
- `SolarUY.Presentation/Components/Pages/AltaParqueSolar.razor`.
- `SolarUY.Presentation/Components/Pages/EditarParqueSolar.razor`.
- `SolarUY.Presentation/Components/Pages/ParquesSolares.razor`.
- `SolarUY.Application.Tests/ParqueSolarServiceTests.cs`, solo preparación de identidad.
- `prompting/req001_alta_parque_solar/specs.md`.
- `prompting/req006_gestion_multiparque/specs.md` e `implementations.md`.

Además de los archivos de cada fila se actualiza el índice de pruebas del README y los documentos REQ005, se enlaza este seguimiento desde la auditoría original y se agrega `followup` a sus resultados JSON. El inventario original conserva su corte y hashes; no se reescribe para ocultar cambios posteriores.

## Pendientes que dependen de vos o del equipo

| Responsable | Acción real pendiente | Qué no se da por realizado |
| --- | --- | --- |
| Administrador y responsables de mantenimiento | Decidir quién debe recibir cada tarea abierta de los datos reales y reasignarla desde la web a un técnico habilitado. Conservar tareas terminales. | No se aplicaron asignaciones a la base real ni se inventaron equivalencias de usuarios entre realms. |
| Equipo | Acordar distribución de configuración fuera de Git y registrar el arranque y prueba de cada compañero. | REQ003 tarea 1.3 continúa abierta. |
| Responsable del despliegue | Definir URL HTTPS pública, configurar los callbacks reales y probar login/logout en producción. | REQ002 tarea 5.3 continúa abierta; no se modificó configuración compartida. |

No hace falta cambiar contratos, relajar el filtro de tareas propias ni alterar historiales para resolver esos pendientes. El código y las instrucciones están disponibles; su ejecución en los entornos correspondientes requiere las decisiones anteriores.

Las correcciones técnicas de esta continuación están verificadas. El estado global no se presenta como 100 %: H-05/H-09 conservan acciones operativas no ejecutadas. Para usar la autorización actualizada en la instancia real corresponde recompilar/reiniciar la aplicación .NET; no es necesario recrear contenedores ni borrar volúmenes.
