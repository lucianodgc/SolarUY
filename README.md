# SolarUY

Aplicación del Taller .NET para gestionar parques solares, nodos, inversores, paneles y mantenimiento; observar generación y salud; consultar históricos/reportes; y realizar consultas mediante un chatbot de solo lectura.

Este README presenta el proyecto y permite ubicar el arranque y la documentación. Los procedimientos completos se mantienen en `docs/`, con enlaces a los detalles específicos de cada integración.

## Documentación

| Necesito… | Documento |
| --- | --- |
| Instalar, configurar secretos y arrancar en Docker o local | [Configuración y ejecución](docs/Configuracion.md) |
| Entender capas, patrones, tácticas, diagramas y archivos | [Arquitectura](docs/Arquitectura.md) |
| Probar funciones, disponibilidad, chatbot y SonarQube | [Pruebas y calidad](docs/Revision_Y_Guia_De_Pruebas.md) |
| Consultar requisitos, tareas y evidencia de una funcionalidad | [Requerimientos](prompting/) |

## Funcionalidades y permisos

| Función | Administrador | Tecnico | Consulta |
| --- | --- | --- | --- |
| Gestión de parques y nodos | Consultar y administrar | Sin gestión administrativa | Sin gestión administrativa |
| Paneles | Consultar, crear, editar, trasladar y dar de baja | Consultar | Consultar |
| Monitoreo, salud e histórico | Consultar | Consultar | Consultar |
| Reportes de generación/mantenimiento y CSV | Consultar y exportar | Consultar y exportar | Consultar y exportar |
| Mantenimiento | Crear, editar, asignar y gestionar tareas | Consultar y cambiar estado de sus tareas | Sin acceso a gestión |
| Órdenes al inversor | Ejecutar con confirmación y auditoría | Consultar estado | Consultar estado |
| Chatbot | Consultar información autorizada | Consultar información autorizada | Consultar información autorizada |

La lectura de paneles, monitoreo o reportes no concede acceso a la gestión de parques. El resumen de mantenimiento realizado de los reportes tampoco habilita consultar tareas pendientes ajenas. El chatbot conserva los permisos de cada servicio y rechaza operaciones de escritura para todos los roles.

## Tecnologías

Versiones declaradas en los proyectos y archivos de despliegue:

| Área | Tecnología |
| --- | --- |
| Plataforma | .NET 8 y C# |
| Interfaz | Blazor Server, Radzen.Blazor 6.0.0, CSS/Bootstrap y JavaScript de apoyo |
| Tiempo real | ASP.NET Core SignalR y WebSockets |
| Persistencia | MariaDB 11.8, EF Core 8.0.31 y Pomelo 8.0.3 |
| Autenticación | Keycloak 26.8.1, OpenID Connect y cookies de ASP.NET Core |
| Validación | FluentValidation 11.11.0 y reglas del dominio |
| Chatbot | Microsoft Agent Framework (`Microsoft.Agents.AI` 1.28.0), MCP Core 2.1.0, Ollama con `qwen3:8b`; OpenAI opcional |
| Simulador | Proceso .NET independiente, TCP y mensajes JSON Lines |
| Despliegue local | Docker Compose, Nginx (`stable`), volúmenes persistentes y phpMyAdmin 5.2.3 |
| Calidad | xUnit, ArchUnitNET, Coverlet/OpenCover, Playwright y SonarQube |

Los `.csproj` y el `package.json` de navegador son la referencia de versiones de paquetes. `global.json` selecciona el SDK 8. Node.js solo es necesario para las pruebas de navegador.

## Arquitectura en breve

Clean Architecture con cuatro capas: **Domain** contiene reglas; **Application**, casos de uso y contratos; **Infrastructure**, persistencia e integraciones; **Presentation**, Blazor y alojamiento de workers. Cada instancia web contiene las cuatro capas. Docker ejecuta dos réplicas detrás de Nginx y comparte MariaDB, Keycloak y el simulador TCP. Ollama se ejecuta en el host.

Los [diagramas de capas, despliegue, datos, login y chatbot](docs/Arquitectura.md), junto con las tácticas y la explicación de carpetas/archivos, están en la guía de arquitectura.

## Configuración inicial

Necesitas .NET SDK 8, PowerShell y Docker Desktop con contenedores Linux. Para migraciones se utiliza `dotnet-ef` 8; para el chatbot local, Ollama con `qwen3:8b`. La [guía completa](docs/Configuracion.md#configuración-inicial) incluye instalación de la herramienta, secretos y direcciones.

1. Si todavía no tienes `.env`, cópialo desde `.env.example` y completa tus contraseñas locales.
2. Restaura dependencias e inicia MariaDB/Keycloak con los comandos siguientes.
3. En Keycloak, completa los dos secretos de cliente y comprueba roles/redirecciones siguiendo la [preparación del realm](docs/Configuracion.md#3-keycloak).
4. Si usarás el chatbot, prepara el modelo y el acceso de Docker a [Ollama](docs/Configuracion.md#4-ollama).

```powershell
dotnet restore SolarUY.sln
docker compose up -d mariadb keycloak phpmyadmin
```

Cada integrante conserva sus secretos fuera de Git. `.env` configura Compose; el modo .NET local necesita user-secrets o variables de entorno.

## Ejecución

Con la configuración inicial terminada, desde la raíz:

```powershell
.\Iniciar-Demo.ps1 -AltaDisponibilidad
```

Abre **http://localhost:18088**. El script aplica migraciones y levanta las dos webs, Nginx y el simulador, conservando datos. Para detener sin borrar volúmenes: `docker compose stop`.

La [guía de ejecución](docs/Configuracion.md#ejecución) incluye puertos, registros, desarrollo local en 5219 y datos de demostración. El restablecimiento de datos es una operación opcional y destructiva; no forma parte del arranque habitual.

## Pruebas

```powershell
dotnet test SolarUY.sln
```

Los casos que requieren servicios pueden omitirse. La [guía de pruebas](docs/Revision_Y_Guia_De_Pruebas.md) explica la base aislada, simulador, proveedor IA real, Playwright, recorrido por roles, caída/actualización de instancias y SonarQube. Una prueba omitida no acredita esa funcionalidad.

## Estado y límites conocidos

El último cierre registrado, del **17/11/2026**, tiene pruebas automatizadas aprobadas y verificaciones manuales pendientes. SonarQube publica el análisis, pero su Gate todavía requiere correcciones. Los resultados, cifras y pendientes se mantienen en una sola ubicación: [estado de pruebas y calidad](docs/Revision_Y_Guia_De_Pruebas.md#estado-y-límites-conocidos).

La configuración de Compose es de desarrollo: HTTP local y Keycloak `start-dev`. La redundancia cubre las webs; las dependencias compartidas y el host no tienen alta disponibilidad en esta pila.

## Cómo mantener la documentación

- **README:** descripción, tecnologías, resumen de arquitectura, arranque y enlaces.
- **docs/Configuracion.md:** configuración completa, modos de ejecución y problemas frecuentes.
- **docs/Arquitectura.md:** diseño, diagramas, decisiones y estructura del código.
- **docs/Revision_Y_Guia_De_Pruebas.md:** procedimientos de prueba, calidad y resultados fechados.
- **prompting/:** requisitos, especificaciones, tareas e implementación por funcionalidad.
- **Documentos junto a integraciones:** procedimientos específicos de [Keycloak](keycloak/TECNICOS.md), [Nginx](deploy/nginx/README.md) y [tests](SolarUY.Integration.Tests/README.md).

Al cambiar un procedimiento, actualiza su documento principal y conserva enlaces desde los demás. Las instrucciones protegidas de `.agents/` se consultan sin modificarlas.
