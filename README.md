# SolarUY

SolarUY es una aplicación web para administrar parques solares y consultar su funcionamiento. Permite gestionar parques y nodos, organizar tareas de mantenimiento y observar la generación de los paneles. El proyecto usa mediciones simuladas para probar el monitoreo y el histórico.

## Funciones principales

- Acceso con perfiles de Administrador, Técnico y Consulta.
- Gestión de parques y nodos, con permisos según el perfil.
- Programación y seguimiento de tareas de mantenimiento.
- Monitoreo en tiempo real y simulación de la comunicación con inversores y paneles.
- Recolección periódica de mediciones simuladas y almacenamiento en la base de datos.
- Consulta del histórico por parque, nodo o panel, comparación de períodos y descarga de reportes CSV.

## Tecnologías

- **C# y .NET 8** para la aplicación y sus servicios.
- **Blazor Server y Radzen** para las páginas y controles de la interfaz.
- **Entity Framework Core 8, Pomelo y MariaDB 11.8** para guardar y consultar los datos.
- **Keycloak y OpenID Connect** para iniciar sesión y asignar permisos por perfil.
- **SignalR** para actualizar el monitoreo en tiempo real y **TCP** para comunicarse con el simulador del inversor.
- **Docker Compose** para iniciar MariaDB, phpMyAdmin y Keycloak en el entorno local.
- **xUnit y ArchUnitNET** para pruebas; hay pruebas de navegador con Playwright.

## Organización del código

La solución separa responsabilidades en proyectos:

| Proyecto | Contenido |
| --- | --- |
| `SolarUY.Presentation` | Interfaz web, autenticación y configuración de la aplicación. |
| `SolarUY.Application` | Servicios, validaciones y contratos de la aplicación. |
| `SolarUY.Domain` | Entidades y reglas del negocio. |
| `SolarUY.Infrastructure` | Acceso a datos e integraciones externas. |
| `SolarUY.InversorSimulador` | Proceso independiente que simula el inversor. |

Las pruebas y sus instrucciones están en los proyectos `*.Tests`; los requerimientos y el seguimiento del desarrollo se encuentran en `prompting/`.

## Inicio local

Se necesita .NET SDK 8 y Docker Desktop con Docker Compose.

1. Crea un archivo `.env` a partir de `.env.example` y completa las credenciales locales.
2. Desde la raíz del proyecto, inicia MariaDB y Keycloak:

   ```powershell
   docker compose up -d
   ```

3. Configura `ConnectionStrings__SolarUY` para que .NET acceda a MariaDB y guarda el secreto del cliente OpenID Connect con User Secrets. Los detalles de Keycloak están en `keycloak/README.md`. El archivo `.env` de Docker no configura automáticamente la aplicación.
4. Inicia el simulador en una terminal:

   ```powershell
   dotnet run --project SolarUY.InversorSimulador/SolarUY.InversorSimulador.csproj
   ```

5. En otra terminal, aplica las migraciones y ejecuta la aplicación:

   ```powershell
   dotnet ef database update --project SolarUY.Infrastructure/SolarUY.Infrastructure.csproj --startup-project SolarUY.Presentation/SolarUY.Presentation.csproj
   dotnet run --project SolarUY.Presentation/SolarUY.Presentation.csproj --launch-profile http
   ```

La aplicación local queda disponible en `http://localhost:5219`. El simulador escucha por defecto en `127.0.0.1:5055`.

## Pruebas

Para ejecutar las pruebas .NET:

```powershell
dotnet test SolarUY.sln
```

Algunas pruebas de integración necesitan una base MariaDB exclusiva de pruebas. La configuración está explicada en [SolarUY.Integration.Tests/README.md](SolarUY.Integration.Tests/README.md). Las pruebas de navegador se ejecutan desde `SolarUY.Browser.Tests` con Playwright.

## Despliegue

La carpeta `deploy/nginx` contiene una configuración de ejemplo para Nginx. La configuración de cada entorno y sus secretos deben mantenerse fuera del repositorio.
