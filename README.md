# SolarUY

SolarUY es una aplicación web para la gestión y el monitoreo de parques solares. El proyecto está en desarrollo: este README distingue las funciones disponibles hoy de las previstas para etapas posteriores.

## Estado actual

El código incluye:

- Aplicación web con .NET 8, Blazor Server y componentes Radzen.
- Autenticación OpenID Connect con Keycloak y autorización por roles (`Administrador`, `Tecnico` y `Consulta`).
- Alta, listado, búsqueda, edición y baja con confirmación de parques solares; acceso restringido al rol `Administrador`.
- Autorización de las seis operaciones de parques en Application mediante `IUsuarioActual`, además de la protección de las páginas. Cada componente conserva su ámbito de datos y recibe la identidad del circuito Blazor.
- Persistencia de parques, nodos, paneles, lecturas y tareas de mantenimiento con Entity Framework Core 8, Pomelo y MariaDB.
- Mantenimiento con programación, edición/reasignación, inicio, finalización y cancelación; filtros por estado, panel, técnico y vencimiento. Las fechas son UTC y las tareas completadas/canceladas son inmutables.
- Inicio y navegación por rol, perfil con identidad/roles/claims y cierre de sesión visible.
- Contrato `IInversorClient` y un simulador en memoria para consultar y cambiar estados de paneles y del volcado a red. La topología predeterminada es `nodo-1` con cuatro paneles; la potencia simulada varía entre las 06:00 y las 18:00, hasta 400 W por panel.
- Histórico de generación por parque, nodo o panel, con consultas diarias, mensuales, anuales y por rango, comparación de períodos y máximos/mínimos en kWh.
- Recolección automática de mediciones simuladas de los paneles registrados, persistidas cada minuto mientras la aplicación está en ejecución, con exclusión entre instancias para evitar duplicados.

El simulador dispone de pantalla: los tres roles consultan y solo Administrador puede activar/desactivar paneles y el volcado. La simulación de esa pantalla es independiente del inventario y su estado se reinicia con el proceso; el recolector mide por separado los paneles persistidos. Quedan fuera del alcance actual el alta de nodos/paneles, reportes y chat con IA. Nginx y el despliegue con varias instancias forman parte de la arquitectura objetivo, pero no están configurados en este repositorio.

### Pantallas disponibles

| Ruta | Función | Acceso |
| --- | --- | --- |
| `/` | Inicio y accesos a módulos | Usuario autenticado |
| `/parques-solares` | Listado, búsqueda, edición y baja | Administrador |
| `/parques-solares/alta` | Registro de parque | Administrador |
| `/parques-solares/{Id}/editar` | Edición de un parque identificado por GUID | Administrador |
| `/mantenimiento` | Tareas y acciones según responsable/rol | Administrador, Técnico |
| `/mantenimiento/crear` | Programación de mantenimiento | Administrador |
| `/simulador` | Estado y potencia simulados | Administrador, Técnico, Consulta; controles solo para Administrador |
| `/historico-generacion` | Energía observada, comparación de períodos y extremos | Administrador, Técnico, Consulta; todos los parques, nodos y paneles |
| `/mi-cuenta` | Identidad, roles y claims | Usuario autenticado |

### Permisos por perfil

| Función | Administrador | Técnico | Consulta |
| --- | --- | --- | --- |
| Gestión de parques | Listar, buscar, crear, editar y eliminar con confirmación | Sin acceso | Sin acceso |
| Mantenimiento | Consultar todas las tareas, programar, editar/reasignar tareas abiertas y cambiar estados permitidos | Consultar e iniciar/completar/cancelar únicamente tareas propias | Sin acceso |
| Simulador | Consultar y activar/desactivar paneles y volcado a red | Solo consultar | Solo consultar |
| Histórico de generación | Consultar todos los parques, nodos y paneles | Consultar todos los parques, nodos y paneles | Consultar todos los parques, nodos y paneles |
| Perfil y sesión | Identidad, roles, claims y logout | Identidad, roles, claims y logout | Identidad, roles, claims y logout |

Los permisos de parques, mantenimiento, simulador e histórico se comprueban en los servicios de Application; no dependen únicamente de ocultar botones. Los técnicos disponibles para asignación se consultan en Keycloak mediante una cuenta de servicio de solo lectura.

La [verificación del entorno habitual](prompting/verificacion_entorno_habitual.md) registra los últimos recorridos locales. La [integración del frontend](prompting/frontend_integracion.md) conserva los antecedentes y su alcance.

### Histórico de generación

Las consultas usan fechas de Uruguay (`America/Montevideo`) y rangos con fin exclusivo. La vista diaria agrupa por hora; la mensual y los rangos, por día; la anual, por mes. Los cálculos se agregan en MariaDB y los resultados muestran energía observada y cobertura completa, parcial o sin datos. Los huecos de medición no se consideran generación cero.

El recolector comienza cuando la web termina de arrancar y usa la potencia nominal y el estado de los paneles registrados. La fuente es simulada; las lecturas quedan guardadas entre reinicios. No se reconstruyen períodos anteriores a su activación ni las mediciones omitidas mientras la aplicación estuvo detenida. Para ver los primeros resultados, consultar el día actual; la cobertura será parcial hasta disponer de mediciones para todo el período.

## Arquitectura

La solución aplica una separación por capas:

| Proyecto | Responsabilidad |
| --- | --- |
| `SolarUY.Presentation` | Interfaz Blazor Server, autenticación y composición de la aplicación. |
| `SolarUY.Application` | Servicios de aplicación, validaciones, contratos e interfaces de integración. |
| `SolarUY.Domain` | Entidades y reglas de negocio. |
| `SolarUY.Infrastructure` | Persistencia EF Core/MariaDB e implementación del simulador. |

Las pruebas están en `SolarUY.Domain.Tests`, `SolarUY.Application.Tests`, `CodeQuality.Tests` y `SolarUY.Integration.Tests`. La suite de integración del simulador usa reloj e identidad controlados y no necesita servicios externos. Los documentos de requerimientos, especificaciones y tareas están bajo `prompting/`.

## Tecnologías

- .NET SDK 8 (la solución fija `8.0.100` en `global.json`, con actualización automática al último feature band disponible).
- Blazor Server y Radzen Blazor 6.
- MariaDB 11.8 y Entity Framework Core 8 con Pomelo 8.0.3.
- Keycloak 26.8.1 con OpenID Connect.
- Docker Compose para MariaDB, phpMyAdmin y Keycloak locales.

## Requisitos previos

- .NET SDK 8.
- Docker y Docker Compose.
- Una instancia MariaDB 11.8 accesible si se va a ejecutar la aplicación con persistencia.

`compose.yaml` incluye MariaDB con volumen persistente, phpMyAdmin y Keycloak. Las credenciales se proporcionan desde `.env` no versionado.

## Inicio local

### 1. Configurar Keycloak

Desde la raíz del repositorio, copia `.env.example` como `.env` o completa tu archivo existente (está excluido de Git) con estas variables requeridas por Compose:

```dotenv
KEYCLOAK_ADMIN_USERNAME=
KEYCLOAK_ADMIN_PASSWORD=
KEYCLOAK_TEST_ADMIN_PASSWORD=
KEYCLOAK_TEST_TECH_PASSWORD=
KEYCLOAK_TEST_VIEWER_PASSWORD=
MARIADB_DATABASE=
MARIADB_USER=
MARIADB_PASSWORD=
MARIADB_ROOT_PASSWORD=
```

Usa valores locales propios; no guardes credenciales reales en archivos versionados. Después inicia los servicios:

```bash
docker compose up -d
```

Compose importa el realm `solaruy` desde `keycloak/realm/solaruy-realm.json`. El cliente configurado es `solaruy-web`. Los usuarios de prueba son `admin.test`, `tecnico.test` y `consulta.test`; sus contraseñas son las variables de entorno correspondientes.

La importación inicial no actualiza automáticamente un realm que ya existe. Para preparar el cliente de directorio en un entorno existente, seguir [keycloak/TECNICOS.md](keycloak/TECNICOS.md); no es necesario borrar el realm o los volúmenes.

### 2. Configurar la aplicación

Define la conexión a MariaDB fuera del repositorio con las credenciales de tu entorno. .NET no lee automáticamente el `.env` de Compose; usá user-secrets o variables de entorno. Por ejemplo, en PowerShell:

```powershell
$env:ConnectionStrings__SolarUY = "Server=localhost;Port=3306;Database=solaruy;User=solaruy;Password=<tu-clave>"
```

Desde la raíz, guarda el secreto confidencial del cliente OIDC en User Secrets:

```bash
dotnet user-secrets --project SolarUY.Presentation/SolarUY.Presentation.csproj set "Authentication:OpenIdConnect:ClientSecret" "<secreto-del-cliente>"
```

La autoridad local y el identificador del cliente se encuentran en `SolarUY.Presentation/appsettings.json`. El proveedor local de Keycloak está disponible en `http://localhost:8080`.

Para cargar y validar técnicos de mantenimiento se necesita además el cliente `solaruy-tecnico-directory`, su cuenta de servicio con permisos de lectura y su secreto externo. Prepararlo según [keycloak/TECNICOS.md](keycloak/TECNICOS.md). Es un cliente distinto de `solaruy-web`; sus secretos no son intercambiables.

### 3. Base de datos y ejecución

Con la cadena de conexión configurada, aplicar las migraciones existentes con Entity Framework Core:

```bash
dotnet ef database update --project SolarUY.Infrastructure/SolarUY.Infrastructure.csproj --startup-project SolarUY.Presentation/SolarUY.Presentation.csproj
```

El historial conserva `AddParqueSolar`, `AddMantenimientoPaneles` y `AddLecturaPanel`; esta última crea la tabla de lecturas utilizada por el histórico y el recolector. Se comprobó su aplicación sobre una base vacía; la aplicación no ejecuta migraciones automáticamente al arrancar. Si no hay migraciones pendientes, no hace falta recrear la base.

`solaruy.sql` es un volcado histórico, no el mecanismo de aprovisionamiento de la aplicación. Sus nombres de tablas coinciden con las migraciones, incluyendo mayúsculas. Conserva responsables de ejemplo o de otro entorno: importarlo no asigna automáticamente las tareas a los usuarios del Keycloak de destino. No debe reemplazar una base existente para intentar corregir un problema de carga.

Para iniciar la aplicación con el perfil HTTP:

```bash
dotnet run --project SolarUY.Presentation/SolarUY.Presentation.csproj --launch-profile http
```

Los perfiles de desarrollo están configurados para `http://localhost:5219` y `https://localhost:7271`.

Si la aplicación estaba ejecutándose mientras se modificaba el código, detenerla con Ctrl+C y volver a ejecutar el comando anterior para cargar la compilación actual. Para estos cambios de código no hace falta reiniciar Docker ni borrar volúmenes.

## Configuración relevante

- `ConnectionStrings__SolarUY`: cadena de conexión de MariaDB.
- `MariaDB__ServerVersion`: versión de servidor MariaDB; por defecto, `11.8.0`.
- `Authentication__OpenIdConnect__Authority`: URL del realm de Keycloak.
- `Authentication__OpenIdConnect__ClientId`: cliente OIDC.
- `Authentication__OpenIdConnect__ClientSecret`: secreto confidencial del cliente; usar User Secrets localmente y una fuente segura de secretos en producción.
- `Keycloak__Directory__BaseUrl`, `Keycloak__Directory__Realm` y `Keycloak__Directory__ClientId`: ubicación del directorio de técnicos y cliente de cuenta de servicio.
- `Keycloak__Directory__ClientSecret`: secreto externo del cliente de directorio; user-secrets en desarrollo y variables de entorno en despliegue.
- `InversorSimulador__Nodos__<identificador>`: lista de paneles simulados del nodo, separados por comas; si no se configura, se utiliza la topología predeterminada.
- `InversorSimulador__PanelesConFalla`: identificadores de panel que el simulador debe marcar como fallados.
- `RecoleccionLecturas__Habilitada`: activa la recolección automática; por defecto, `true`.
- `RecoleccionLecturas__Intervalo`: intervalo de recolección; por defecto, `00:01:00`, configurable entre un segundo y cinco minutos. Se guarda como máximo una lectura por panel/minuto. Reiniciar la aplicación después de modificar estas opciones.

## Pruebas

Para ejecutar las pruebas de la solución:

```bash
dotnet test SolarUY.sln
```

Las pruebas arquitectónicas están en `CodeQuality.Tests`. El simulador y sus permisos tienen [19 casos permanentes de integración](SolarUY.Integration.Tests/README.md), incluidos en la solución. La evidencia adicional de navegador y persistencia está en [la auditoría](prompting/auditoria_integral.md) y [su seguimiento](prompting/seguimiento_auditoria.md).

El histórico y el recolector tienen pruebas de integración con MariaDB 11.8. Requieren `SOLARUY_HISTORICO_TEST_CONNECTION` apuntando a una base exclusiva de pruebas cuyo nombre comience con `solaruy_req007_test`; el fixture reinicia sus datos. Sin esta variable, esos casos se omiten. La preparación se detalla en [SolarUY.Integration.Tests/README.md](SolarUY.Integration.Tests/README.md).

Resultados registrados el **22/10/2026** para REQ007:

- Regresión funcional con MariaDB aislada: **122 pruebas correctas, sin omisiones**, excluyendo volumen.
- Corrección posterior del arranque: **9 controles arquitectónicos y 16 casos del recolector correctos**, incluido el fallo por puerto ocupado. La solución completa no se repitió tras esa corrección.
- **Pendiente de rendimiento:** el escenario de un millón de lecturas y cuatro consultas concurrentes alcanzó **5,487 s** en parque/mes frente al límite de 5 s. La tarea 6.8 sigue abierta; ver [el registro de implementación](prompting/req007_Hist%C3%B3rico_generaci%C3%B3n/implementations.md).

La suite permanente del simulador no necesita Docker ni credenciales. Para comprobar el proyecto mientras otra instancia mantiene sus binarios en uso, se puede utilizar una salida separada y ejecución secuencial:

```powershell
dotnet test SolarUY.sln -m:1 -p:BuildInParallel=false -p:BaseOutputPath=bin/VerificacionLocal/
```

## Verificación local y pendientes

La [última verificación del entorno habitual](prompting/verificacion_entorno_habitual.md) comprobó compilación, arranque y recorridos con los tres roles usando MariaDB y Keycloak reales. Se verificaron consultas, búsqueda, carga de formularios, perfil, permisos y consulta del simulador en HTTP 5219 y en una compilación actual en HTTPS 7271, sin fallos detectados de DI, autenticación, circuito Blazor o JavaScript en esos recorridos.

En esa revisión no se ejecutaron guardados, bajas, cambios de estado ni reasignaciones sobre datos reales. El contenido de las cuatro tablas del modelo permaneció idéntico. Las pruebas de escritura e integridad en recursos aislados están documentadas en las auditorías.

Quedan pendientes acciones operativas:

- **Asignaciones de mantenimiento:** `ResponsableId` debe coincidir con el `sub` de un técnico habilitado en el realm correspondiente. En la verificación del 22/10/2026, el diagnóstico abierto de `SOL-MAL-003` conserva `TECNICO-PRUEBA-002` y requiere que el Administrador/equipo decida y seleccione al técnico correcto. Las otras cuatro tareas con responsables de ejemplo son terminales y conservan su historial. La tarea de `SOL-ROC-001` sí está asignada a `tecnico.test`. Procedimiento en [keycloak/TECNICOS.md](keycloak/TECNICOS.md).
- **Preparación del equipo:** acordar la distribución de configuración fuera de Git y registrar el arranque efectivo de cada compañero; REQ003 tarea 1.3 sigue abierta.
- **Producción:** definir la URL HTTPS pública, configurar los callbacks reales y verificar login/logout en ese entorno; REQ002 tarea 5.3 sigue abierta.

El alcance local comprobado puede utilizarse para una presentación, con la compilación actual y los pendientes declarados. Las verificaciones locales no acreditan un despliegue de producción ni el arranque en otros equipos.

## Documentación del proyecto

La documentación funcional y técnica está organizada por requerimiento dentro de [`prompting/`](prompting/). Cada carpeta reúne el alcance de una funcionalidad y su estado de trabajo:

1. **Requerimientos (`requirements.md`)**: describe la necesidad y los criterios de aceptación. Es la referencia para entender qué se espera del sistema.
2. **Especificaciones (`specs.md`)**: registra el diseño acordado, las decisiones técnicas y las aclaraciones que aún hacen falta antes de implementar.
3. **Tareas (`tasks.md`)**: desglosa el trabajo por capas e indica su estado con casillas completadas o pendientes.
4. **Implementación (`implementacion.md` o `implementations.md`)**: cuando existe, resume lo realizado, los archivos afectados y la verificación registrada.

### Requerimientos documentados

| Carpeta | Tema | Estado resumido |
| --- | --- | --- |
| [`req001_alta_parque_solar`](prompting/req001_alta_parque_solar/) | Entidad, persistencia, listado y alta de parques solares. | Funcionalidad implementada; incluye requerimientos, especificaciones, tareas e informe de implementación. |
| [`req002_login_keycloak`](prompting/req002_login_keycloak/) | Inicio y cierre de sesión con OpenID Connect, roles y Keycloak local. | Flujo de autenticación y autorización local documentado como verificado; quedan aspectos de configuración de producción pendientes. |
| [`req003_infraestructura_base_de_datos`](prompting/req003_infraestructura_base_de_datos/) | MariaDB, contexto EF Core y migraciones. | Infraestructura utilizada por parques y mantenimiento; no requiere pantalla propia. Ver tareas operativas pendientes. |
| [`req004_Mantenimiento_de_paneles`](prompting/req004_Mantenimiento_de_paneles/) | Mantenimiento de paneles. | Implementado con permisos por responsable, estados y filtros; quedan asignaciones de datos locales por revisar. El requerimiento conserva el nombre `requierements.md`. |
| [`req005_Simulador del inversor`](prompting/req005_Simulador%20del%20inversor/) | Contrato y simulador sustituible de inversor. | Pantalla de consulta/control, autorización en Application y 19 casos permanentes de integración. |
| [`req006_gestion_multiparque`](prompting/req006_gestion_multiparque/) | Gestión multiparque. | ABM y búsqueda, confirmación de baja y autorización administrativa en las seis operaciones del servicio. |
| [`req007_Histórico_generación`](prompting/req007_Hist%C3%B3rico_generaci%C3%B3n/) | Histórico de energía y recolección periódica. | Consultas, comparación, extremos y recolección del inventario implementados; pendiente el umbral de rendimiento de la tarea 6.8. |

Los estados de esta tabla son una orientación general. Para conocer el detalle y los bloqueos vigentes, prevalecen las casillas y notas de `tasks.md` y el informe de implementación de cada carpeta.

### Referencias de configuración y arquitectura

- [Verificación del entorno habitual](prompting/verificacion_entorno_habitual.md): resultado local más reciente, tareas existentes y límites de las comprobaciones.
- [Seguimiento de auditoría](prompting/seguimiento_auditoria.md): cierre técnico de H-06/H-07/H-10 y pendientes operativos H-05/H-09.
- [Auditoría integral](prompting/auditoria_integral.md): criterios por requerimiento, problemas encontrados y evidencia original; incluye enlaces al inventario y resultados.

- [`keycloak/README.md`](keycloak/README.md): configuración local de Keycloak, realm, cliente OIDC y usuarios de prueba.
- [`.agents/rules/`](.agents/rules/): reglas del proyecto para arquitectura por capas, principios de diseño, nomenclatura, modelo de dominio, presentación, despliegue, tecnologías autorizadas y modelo de datos.
- [`compose.yaml`](compose.yaml): servicios disponibles actualmente en Docker Compose.
- [`global.json`](global.json) y los archivos `.csproj`: versión de SDK, frameworks y dependencias del proyecto.

Al implementar una funcionalidad, conviene leer primero sus requerimientos y especificaciones, revisar las tareas pendientes y consultar las reglas aplicables en `.agents/rules/`. Al cerrar el trabajo, actualizar las tareas y el informe de implementación correspondiente mantiene el README y el estado del proyecto trazables.
