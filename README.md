# SolarUY

SolarUY es una aplicación web para administrar parques solares y consultar su funcionamiento. Permite gestionar parques, nodos y tareas de mantenimiento, monitorear paneles y consultar su generación. Las lecturas del inversor son simuladas; el proyecto no se conecta a equipos solares reales.

## Qué permite hacer

- Iniciar sesión con los perfiles Administrador, Técnico y Consulta.
- Gestionar parques y nodos según los permisos de cada perfil.
- Programar y seguir tareas de mantenimiento de paneles.
- Consultar el estado del simulador del inversor y monitorear cambios en tiempo real.
- Guardar lecturas simuladas y consultar históricos y reportes CSV.

## Tecnologías

- **C# y .NET 8:** lenguaje y plataforma de la aplicación.
- **Blazor Server y Radzen:** interfaz web interactiva. La aplicación se ejecuta en el servidor; no usa Blazor WebAssembly.
- **Entity Framework Core 8, Pomelo y MariaDB 11.8:** acceso y almacenamiento de los datos.
- **Keycloak con OpenID Connect:** inicio de sesión y roles.
- **SignalR:** conexión interactiva de Blazor y actualizaciones del monitoreo.
- **TCP:** comunicación entre la aplicación y el proceso que simula el inversor.
- **Docker Compose:** entorno local para MariaDB, phpMyAdmin y Keycloak.
- **xUnit, ArchUnitNET y Playwright:** pruebas de código, arquitectura y navegador.

## Cómo se organiza

La solución separa la interfaz, los casos de uso, las reglas del negocio y las conexiones externas:

| Proyecto | Para qué sirve |
| --- | --- |
| `SolarUY.Presentation` | Páginas Blazor, autenticación y configuración de la aplicación. |
| `SolarUY.Application` | Acciones del sistema, validaciones y permisos de negocio. |
| `SolarUY.Domain` | Entidades y reglas propias del negocio. |
| `SolarUY.Infrastructure` | MariaDB, Entity Framework Core y conexiones externas. |
| `SolarUY.InversorSimulador` | Proceso aparte que responde como un inversor simulado. |

## Modelo de datos

El siguiente diagrama resume el modelo actual. El inversor puede no estar registrado en algunos nodos; las lecturas antiguas sin nodo comprobable se conservan con `NodoId` vacío. `ResponsableId` guarda el identificador del usuario de Keycloak, no una relación directa en MariaDB.

<!-- Pegar aquí la imagen original del MER realizada para la primera entrega. -->

## Arquitectura y despliegue

El diagrama muestra la arquitectura por capas y la propuesta de despliegue distribuido de la primera entrega. La configuración de Nginx para dos instancias está disponible como ejemplo en `deploy/nginx`; el arranque local normal usa una instancia. El chat con IA y su servicio externo aparecen como propuesta y **no están implementados en la aplicación actual**.

<!-- Pegar aquí la imagen original del diagrama de arquitectura/despliegue de la primera entrega. -->

En el diseño por capas, Presentation recibe la interacción del navegador; Application aplica los permisos y coordina las acciones; Domain contiene las reglas del negocio; Infrastructure guarda datos o se conecta con servicios externos. Keycloak administra la identidad y los roles. El proceso simulador representa al inversor y se comunica por TCP. Las instancias comparten MariaDB; el mecanismo de recolección evita guardar la misma lectura desde varias instancias.

## Desarrollo con IA y SonarQube

Codex se utilizó como asistente para proponer cambios de código, explicar errores y ayudar a interpretar avisos de SonarQube. Aceleró algunas tareas y nos mostró que generar una propuesta no reemplaza entender y validar el código. Para próximas iteraciones queremos reforzar esa revisión y registrar qué se comprobó.

El análisis compartido mostró **14,9 % de cobertura general**. La cobertura se generó con las pruebas del Dominio: **57 pruebas aprobadas**. SonarQube mostró cobertura en siete archivos del Dominio; el porcentaje general también incluye el resto de SolarUY. `ANALYSIS SUCCESSFUL` indica que el informe se envió al servidor, no que el Quality Gate haya sido aprobado. Los avisos se deben revisar en el panel y contrastar con el código antes de decidir qué hacer.

Los documentos de requerimientos, especificaciones y tareas están en `prompting/`. Los informes de implementación y auditoría guardan detalle técnico; sirven como referencia para consultar una funcionalidad o un hallazgo concreto.

## Inicio local

Se necesita .NET SDK 8 y Docker Desktop con Docker Compose.

1. Crea `.env` a partir de `.env.example` y completa credenciales locales.
2. Desde la raíz, inicia MariaDB y Keycloak:

   ```powershell
   docker compose up -d
   ```

3. Configura la conexión .NET a MariaDB (`ConnectionStrings__SolarUY`) y el secreto del cliente OpenID Connect con User Secrets. Los detalles de Keycloak están en `keycloak/README.md`; `.env` configura Docker, no la aplicación .NET.
4. Inicia el simulador en una terminal:

   ```powershell
   dotnet run --project SolarUY.InversorSimulador/SolarUY.InversorSimulador.csproj
   ```

5. En otra terminal, aplica las migraciones y ejecuta SolarUY:

   ```powershell
   dotnet ef database update --project SolarUY.Infrastructure/SolarUY.Infrastructure.csproj --startup-project SolarUY.Presentation/SolarUY.Presentation.csproj
   dotnet run --project SolarUY.Presentation/SolarUY.Presentation.csproj --launch-profile http
   ```

La aplicación queda en `http://localhost:5219`; el simulador escucha en `127.0.0.1:5055`.

Para ejecutar las pruebas .NET:

```powershell
dotnet test SolarUY.sln
```

Algunas pruebas de integración requieren una base MariaDB exclusiva. Sus instrucciones están en [SolarUY.Integration.Tests/README.md](SolarUY.Integration.Tests/README.md).
