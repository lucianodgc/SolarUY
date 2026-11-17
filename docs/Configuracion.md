# Configuración y ejecución

[Inicio](../README.md) · [Arquitectura](Arquitectura.md) · [Pruebas](Revision_Y_Guia_De_Pruebas.md)

Todos los comandos parten de la raíz en PowerShell. Necesitas .NET SDK 8, Docker Desktop con contenedores Linux y `dotnet-ef` 8. Si falta este último: `dotnet tool install --global dotnet-ef --version 8.0.31`.

## Preparación inicial

1. Si no existe `.env`, copia `.env.example` y completa tus contraseñas locales. Deja inicialmente vacíos los dos secretos de cliente.
2. Restaura e inicia dependencias:

```powershell
dotnet restore SolarUY.sln
docker compose up -d mariadb keycloak phpmyadmin
```

3. Cuando [Keycloak](http://localhost:8080) esté disponible, entra con el administrador de `.env`, selecciona el realm `solaruy` y copia los secretos desde **Clients → Credentials**:
   - `solaruy-web` → `SOLARUY_WEB_CLIENT_SECRET`.
   - `solaruy-tecnico-directory` → `SOLARUY_DIRECTORY_CLIENT_SECRET`.
4. Comprueba los usuarios `admin.test`, `tecnico.test` y `consulta.test`. El cliente de directorio necesita los roles `view-users` y `query-users`: [instrucciones específicas](../keycloak/TECNICOS.md).

Cada integrante conserva sus secretos fuera de Git. El import omite realms existentes: cambiar `.env` no actualiza usuarios/clientes ya creados. `.env` configura Docker; .NET local utiliza user-secrets o variables de entorno.

## Chatbot local

Instala Ollama y ejecuta `ollama pull qwen3:8b`. Para que Docker lo alcance, detén la instancia previa y abre una terminal dedicada:

```powershell
$env:OLLAMA_HOST = "0.0.0.0:11434"
ollama serve
```

Restringe el acceso mediante firewall al entorno de desarrollo. Compose utiliza `host.docker.internal:11434`; la web local puede usar `127.0.0.1:11434`. El resto de la aplicación puede utilizarse sin respuestas del modelo.

## Ejecutar con Docker

```powershell
.\Iniciar-Demo.ps1 -AltaDisponibilidad
```

El script aplica migraciones y levanta dos webs, Nginx y el simulador, conservando datos. Aplicación: **http://localhost:18088**; Keycloak: **8080**; phpMyAdmin: **8081**; MariaDB desde el host: **3307**.

Para revisar estado: `docker compose ps`; para registros: `docker compose logs --tail 100 solaruy-a solaruy-b simulador nginx`. Detén con `docker compose stop`. No uses `down -v` para un cierre normal: elimina los volúmenes.

## Desarrollo con la web local

Mantén MariaDB/Keycloak en Docker y detén las webs Docker antes de usar los mismos datos. Sustituye los ejemplos por tus valores:

```powershell
dotnet user-secrets set "ConnectionStrings:SolarUY" "Server=127.0.0.1;Port=3307;Database=solaruy;User=solaruy;Password=TU_PASSWORD;" --project SolarUY.Presentation
dotnet user-secrets set "Authentication:OpenIdConnect:ClientSecret" "SECRETO_WEB" --project SolarUY.Presentation
dotnet user-secrets set "Keycloak:Directory:ClientSecret" "SECRETO_DIRECTORIO" --project SolarUY.Presentation
dotnet ef database update --project SolarUY.Infrastructure --startup-project SolarUY.Presentation -- --environment Development
```

En una terminal ejecuta `dotnet run --project SolarUY.InversorSimulador`; en otra, `.\Iniciar-Demo.ps1`. Web: **http://localhost:5219**; TCP: **127.0.0.1:5055**. Finaliza con Ctrl+C.

## Datos y problemas habituales

En una base vacía, Administrador crea parque, nodos/inversores y paneles. Las tareas se asignan a técnicos reales del realm. `-ReiniciarDatos` es opcional y reemplaza datos de demostración; no se usa en el arranque habitual.

Si falla login, revisa secreto y redirecciones de Keycloak. Si falla el chat, revisa `ollama list`, dirección y firewall. Si faltan mediciones, revisa inventario, simulador y logs; el histórico necesita esperar muestras. Los procedimientos específicos están en [Keycloak](../keycloak/TECNICOS.md) y [Nginx](../deploy/nginx/README.md).
