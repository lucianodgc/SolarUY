# SolarUY

Aplicación del Taller .NET para administrar parques solares, nodos, paneles y mantenimiento, consultar generación y salud, generar reportes y conversar con un chatbot de solo lectura.

## Tecnologías y arquitectura

.NET 8, Blazor Server/Radzen, SignalR, MariaDB 11.8 con EF Core/Pomelo, Keycloak/OpenID Connect, Docker/Nginx y Ollama (`qwen3:8b`) con Agent Framework y MCP. Las versiones de paquetes están en los `.csproj`.

La aplicación sigue Clean Architecture en cuatro capas y se ejecuta en dos instancias detrás de Nginx. Los [diagramas y las decisiones principales](docs/Arquitectura.md) explican el diseño.

## Funcionalidades

- **Administrador:** gestiona inventario, asigna mantenimiento y envía órdenes al inversor.
- **Tecnico:** consulta el sistema y gestiona sus tareas asignadas.
- **Consulta:** accede a paneles, monitoreo, salud, histórico, reportes y chatbot según sus permisos.

El chatbot solo consulta información autorizada; no modifica datos ni ejecuta órdenes.

## Iniciar

Necesitas .NET SDK 8, PowerShell y Docker Desktop. Prepara una vez `.env`, los dos secretos de Keycloak y Ollama siguiendo [Configuración](docs/Configuracion.md). Después, desde la raíz:

```powershell
.\Iniciar-Demo.ps1 -AltaDisponibilidad
```

Abre **http://localhost:18088**. Para detener conservando datos: `docker compose stop`.

## Documentación

| Documento | Contenido |
| --- | --- |
| [Configuración](docs/Configuracion.md) | Preparación por integrante, Docker y desarrollo local. |
| [Arquitectura](docs/Arquitectura.md) | Capas, decisiones, tácticas y los dos diagramas. |
| [Pruebas y calidad](docs/Revision_Y_Guia_De_Pruebas.md) | Recorrido funcional, pruebas automáticas, SonarQube y pendientes. |
| [Requerimientos](prompting/) | Especificaciones, tareas y evidencia por funcionalidad. |

La configuración incluida es de desarrollo. La redundancia cubre las webs; las dependencias compartidas no están replicadas.
