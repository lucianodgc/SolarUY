# SolarUY

SolarUY es una aplicación web para la gestión y el monitoreo de parques solares. El proyecto está en desarrollo: este README distingue las funciones disponibles hoy de las previstas para etapas posteriores.

## Estado actual

El código incluye:

- Aplicación web con .NET 8, Blazor Server y componentes Radzen.
- Autenticación OpenID Connect con Keycloak y autorización por roles (`Administrador`, `Tecnico` y `Consulta`).
- Alta y listado de parques solares, con nombre único, ubicación y capacidad estimada opcional. Estas páginas están restringidas al rol `Administrador`.
- Persistencia de parques con Entity Framework Core 8, Pomelo y MariaDB; se incluye la migración inicial para `ParqueSolar`.
- Contrato `IInversorClient` y un simulador en memoria para consultar y cambiar estados de paneles y del volcado a red. La topología predeterminada es `nodo-1` con cuatro paneles; la potencia simulada varía entre las 06:00 y las 18:00, hasta 400 W por panel.

La aplicación todavía no implementa la gestión de nodos y paneles, mantenimiento, históricos y reportes, ni el chat con IA. El simulador es un componente de infraestructura: no hay todavía una pantalla para operarlo ni persiste su estado entre reinicios. Nginx y el despliegue con varias instancias forman parte de la arquitectura objetivo, pero no están configurados en este repositorio.

## Arquitectura

La solución aplica una separación por capas:

| Proyecto | Responsabilidad |
| --- | --- |
| `SolarUY.Presentation` | Interfaz Blazor Server, autenticación y composición de la aplicación. |
| `SolarUY.Application` | Servicios de aplicación, validaciones, contratos e interfaces de integración. |
| `SolarUY.Domain` | Entidades y reglas de negocio. |
| `SolarUY.Infrastructure` | Persistencia EF Core/MariaDB e implementación del simulador. |

Las pruebas están en `SolarUY.Domain.Tests`, `SolarUY.Application.Tests` y `CodeQuality.Tests`. Los documentos de requerimientos, especificaciones y tareas están bajo `prompting/`.

## Tecnologías

- .NET SDK 8 (la solución fija `8.0.100` en `global.json`, con actualización automática al último feature band disponible).
- Blazor Server y Radzen Blazor 6.
- MariaDB 11.8 y Entity Framework Core 8 con Pomelo 8.0.3.
- Keycloak 26.8.1 con OpenID Connect.
- Docker Compose para el entorno local de Keycloak.

## Requisitos previos

- .NET SDK 8.
- Docker y Docker Compose.
- Una instancia MariaDB 11.8 accesible si se va a ejecutar la aplicación con persistencia.

> `compose.yaml` levanta actualmente Keycloak, pero todavía no incluye un servicio MariaDB. La configuración y el aprovisionamiento de MariaDB están pendientes en los requisitos de infraestructura.

## Inicio local

### 1. Configurar Keycloak

Desde la raíz del repositorio, crea o completa el archivo `.env` (está excluido de Git) con estas variables requeridas por Compose:

```dotenv
KEYCLOAK_ADMIN_USERNAME=
KEYCLOAK_ADMIN_PASSWORD=
KEYCLOAK_TEST_ADMIN_PASSWORD=
KEYCLOAK_TEST_TECH_PASSWORD=
KEYCLOAK_TEST_VIEWER_PASSWORD=
```

Usa valores locales propios; no guardes credenciales reales en archivos versionados. Después inicia Keycloak:

```bash
docker compose up -d keycloak
```

Compose importa el realm `solaruy` desde `keycloak/realm/solaruy-realm.json`. El cliente configurado es `solaruy-web`. Los usuarios de prueba son `admin.test`, `tecnico.test` y `consulta.test`; sus contraseñas son las variables de entorno correspondientes.

### 2. Configurar la aplicación

Configura una instancia MariaDB 11.8 y define la cadena de conexión fuera del repositorio. Por ejemplo, en PowerShell:

```powershell
$env:ConnectionStrings__SolarUY = "Server=localhost;Port=3306;Database=solaruy;User=solaruy;Password=<tu-clave>"
```

Guarda el secreto confidencial del cliente OIDC en User Secrets desde la carpeta del proyecto de presentación:

```bash
dotnet user-secrets --project SolarUY.Presentation/SolarUY.Presentation.csproj set "Authentication:OpenIdConnect:ClientSecret" "<secreto-del-cliente>"
```

La autoridad local y el identificador del cliente se encuentran en `SolarUY.Presentation/appsettings.json`. El proveedor local de Keycloak está disponible en `http://localhost:8080`.

### 3. Base de datos y ejecución

La migración inicial se puede aplicar con Entity Framework Core (con la cadena de conexión ya configurada):

```bash
dotnet ef database update --project SolarUY.Infrastructure/SolarUY.Infrastructure.csproj --startup-project SolarUY.Presentation/SolarUY.Presentation.csproj
```

Para iniciar la aplicación con el perfil HTTP:

```bash
dotnet run --project SolarUY.Presentation/SolarUY.Presentation.csproj --launch-profile http
```

Los perfiles de desarrollo están configurados para `http://localhost:5219` y `https://localhost:7271`.

> **Estado del arranque:** la documentación de implementación registra que falta `App.razor` en `SolarUY.Presentation/Components`, requerido por el host Blazor, y que la compilación integral estaba bloqueada por esa ausencia. Por eso, el comando de ejecución puede requerir que se resuelva primero ese pendiente.

## Configuración relevante

- `ConnectionStrings__SolarUY`: cadena de conexión de MariaDB.
- `MariaDB__ServerVersion`: versión de servidor MariaDB; por defecto, `11.8.0`.
- `Authentication__OpenIdConnect__Authority`: URL del realm de Keycloak.
- `Authentication__OpenIdConnect__ClientId`: cliente OIDC.
- `Authentication__OpenIdConnect__ClientSecret`: secreto confidencial del cliente; usar User Secrets localmente y una fuente segura de secretos en producción.
- `InversorSimulador__PanelesConFalla`: identificadores de panel que el simulador debe marcar como fallados.

## Pruebas

Para ejecutar las pruebas de la solución:

```bash
dotnet test SolarUY.sln
```

Las pruebas arquitectónicas están en `CodeQuality.Tests`. La lógica del simulador aún tiene pendiente un mecanismo de verificación compatible con las reglas actuales del proyecto.

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
| [`req003_infraestructura_base_de_datos`](prompting/req003_infraestructura_base_de_datos/) | MariaDB en Docker, contexto EF Core y migraciones. | Parcial: hay contexto y migración para `ParqueSolar`, pero Compose aún no levanta MariaDB y faltan decisiones/modelado para nodos y paneles. Consultar las tareas pendientes antes de asumir que el requerimiento está cerrado. |
| [`req004_Priorización_y_documentación_de_requerimientos`](prompting/req004_Priorización_y_documentación_de_requerimientos/) | Mantenimiento de paneles. | Especificaciones y tareas describen trabajo futuro; las tareas están pendientes. El archivo de requerimientos conserva el nombre `requierements.md`. |
| [`req005_Simulador del inversor`](prompting/req005_Simulador%20del%20inversor/) | Contrato y simulador sustituible de inversor. | Implementado en Application e Infrastructure; la verificación automatizada específica del simulador sigue pendiente según las notas del requerimiento. |

Los estados de esta tabla son una orientación general. Para conocer el detalle y los bloqueos vigentes, prevalecen las casillas y notas de `tasks.md` y el informe de implementación de cada carpeta.

### Referencias de configuración y arquitectura

- [`keycloak/README.md`](keycloak/README.md): configuración local de Keycloak, realm, cliente OIDC y usuarios de prueba.
- [`.agents/rules/`](.agents/rules/): reglas del proyecto para arquitectura por capas, principios de diseño, nomenclatura, modelo de dominio, presentación, despliegue, tecnologías autorizadas y modelo de datos.
- [`compose.yaml`](compose.yaml): servicios disponibles actualmente en Docker Compose.
- [`global.json`](global.json) y los archivos `.csproj`: versión de SDK, frameworks y dependencias del proyecto.

Al implementar una funcionalidad, conviene leer primero sus requerimientos y especificaciones, revisar las tareas pendientes y consultar las reglas aplicables en `.agents/rules/`. Al cerrar el trabajo, actualizar las tareas y el informe de implementación correspondiente mantiene el README y el estado del proyecto trazables.
