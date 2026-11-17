# Configurar y ejecutar SolarUY

[Inicio](../README.md) · [Configuración](Configuracion.md) · [Arquitectura](Arquitectura.md) · [Pruebas y calidad](Revision_Y_Guia_De_Pruebas.md)

Procedimiento completo por integrante: secretos, Keycloak, Ollama, Docker y desarrollo local. Todos los comandos parten de la raíz del repositorio en PowerShell.

## Configuración inicial

### 1. Requisitos del equipo

- .NET SDK 8, PowerShell y Docker Desktop con contenedores Linux.
- `dotnet-ef` 8 para migraciones. Si falta: `dotnet tool install --global dotnet-ef --version 8.0.31`.
- Ollama con `qwen3:8b` para el chatbot local; el resto de la aplicación puede utilizarse sin respuestas del modelo.
- Node.js/npm y Chromium de Playwright para las pruebas de navegador; servidor SonarQube y token propio para el análisis.

### 2. Variables y dependencias

Si todavía no existe `.env`:

```powershell
Copy-Item .env.example .env
```

Completa contraseñas de MariaDB, administrador de Keycloak y usuarios de prueba. Deja inicialmente vacíos los dos secretos de cliente: los genera Keycloak. Cada integrante mantiene sus propios valores fuera de Git; conserva un `.env` que ya esté preparado.

```powershell
dotnet restore SolarUY.sln
docker compose up -d mariadb keycloak phpmyadmin
docker compose ps
```

Espera a que Keycloak termine de iniciar. `.env` configura Compose, no una ejecución directa de .NET; para esta última se utilizan user-secrets o variables de entorno.

### 3. Keycloak

1. Abre [Keycloak](http://localhost:8080) con `KEYCLOAK_ADMIN_USERNAME` / `KEYCLOAK_ADMIN_PASSWORD` de `.env` y selecciona el realm `solaruy`.
2. En **Clients → solaruy-web → Credentials**, copia el secreto a `SOLARUY_WEB_CLIENT_SECRET` de `.env`.
3. Copia el de `solaruy-tecnico-directory` a `SOLARUY_DIRECTORY_CLIENT_SECRET`. Su cuenta de servicio necesita únicamente `realm-management / view-users` y `query-users`.
4. Comprueba `admin.test`, `tecnico.test` y `consulta.test`, sus roles y contraseñas locales. Revisa las redirecciones de login/logout de `solaruy-web` para 18088; el import también contempla 5219 y 7271.

El import inicial omite realms existentes: cambiar `.env` no actualiza usuarios/clientes ya creados. Si falta el cliente de directorio en un realm existente, importa `keycloak/clients/solaruy-tecnico-directory.json` y asigna sus permisos siguiendo [TECNICOS.md](../keycloak/TECNICOS.md), conservando el realm.

### 4. Ollama

```powershell
ollama pull qwen3:8b
```

Para las webs Docker, Ollama debe escuchar en una interfaz alcanzable desde contenedores. Con la instancia anterior detenida, abre una terminal dedicada:

```powershell
$env:OLLAMA_HOST = "0.0.0.0:11434"
ollama serve
```

Restringe el acceso mediante firewall al entorno local/Docker. Compose usa `http://host.docker.internal:11434/` con permiso explícito para ese hostname. Con la web fuera de Docker puede mantenerse `127.0.0.1:11434`. El contexto configurado es 16384; memoria y modelo afectan la latencia.

OpenAI es opcional: necesita `Chatbot:Proveedor=OpenAI`, un `Chatbot:Modelo` compatible y `Chatbot:OpenAI:ApiKey` en configuración externa. Tiene consumo propio del proveedor y no es necesario para Ollama.

## Ejecución

### Pila completa con dos instancias

```powershell
.\Iniciar-Demo.ps1 -AltaDisponibilidad
```

El script valida variables, inicia dependencias, espera MariaDB, aplica migraciones desde el host y construye/levanta las webs, simulador y Nginx. Conserva los datos. Keycloak debe estar disponible antes de intentar login.

| Servicio | Dirección local predeterminada |
| --- | --- |
| Aplicación mediante Nginx | http://localhost:18088 |
| Keycloak | http://localhost:8080 |
| phpMyAdmin | http://localhost:8081 |
| MariaDB desde el host | `127.0.0.1:3307` |
| Ollama en el host | http://localhost:11434 |
| Simulador Docker | `simulador:5055`, sin publicación al host |

```powershell
docker compose ps
docker compose logs --tail 100 solaruy-a solaruy-b simulador nginx
docker compose stop
```

`stop` conserva volúmenes. `docker compose up -d --build` reconstruye y arranca, pero no sustituye las migraciones del script. `docker compose down -v` elimina volúmenes: no es un cierre normal.

### Desarrollo con la web fuera de Docker

Utiliza MariaDB/Keycloak del Compose. Si estaban activas las webs Docker, detén `solaruy-a`, `solaruy-b` y `nginx` antes de usar los mismos datos desde la web local. Configura secretos sustituyendo los ejemplos:

```powershell
dotnet user-secrets set "ConnectionStrings:SolarUY" "Server=127.0.0.1;Port=3307;Database=solaruy;User=solaruy;Password=TU_PASSWORD;" --project SolarUY.Presentation
dotnet user-secrets set "Authentication:OpenIdConnect:ClientSecret" "SECRETO_WEB" --project SolarUY.Presentation
dotnet user-secrets set "Keycloak:Directory:ClientSecret" "SECRETO_DIRECTORIO" --project SolarUY.Presentation
dotnet ef database update --project SolarUY.Infrastructure --startup-project SolarUY.Presentation -- --environment Development
```

Refleja en la conexión cualquier nombre de base/usuario distinto elegido en `.env`. Abre dos terminales:

```powershell
# Terminal 1: simulador local en 127.0.0.1:5055
dotnet run --project SolarUY.InversorSimulador
```

```powershell
# Terminal 2: web en http://localhost:5219
.\Iniciar-Demo.ps1
```

Detén procesos con Ctrl+C. El simulador recibe el inventario desde la aplicación y no necesita conexión propia a MariaDB.

### Datos iniciales

En una base vacía, Administrador crea parque, nodos con inversor y paneles antes de observar generación. El arranque no importa respaldos ni inventario automáticamente.

`Iniciar-Demo.ps1 -ReiniciarDatos` importa `solaruy_pruebas.sql` y **restablece datos de la base configurada**: usar solo si se quieren reemplazar los datos de demostración. No se combina con `-AltaDisponibilidad`. Las tareas abiertas deben asignarse desde la aplicación a técnicos cuyo `sub` pertenezca al realm local.

## Problemas frecuentes

| Síntoma | Revisar |
| --- | --- |
| Falta conexión MariaDB | `.env` para Docker; user-secrets o `ConnectionStrings__SolarUY` para .NET. No hay contraseña de respaldo en `appsettings.json`. |
| Login/callback fallido | Keycloak disponible, realm/cliente, secreto y redirecciones del puerto usado. Importar no actualiza realms existentes. |
| Directorio de técnicos inaccesible | Cliente de servicio, secreto, permisos `view-users`/`query-users` y técnicos habilitados. Lista vacía puede ser válida. |
| Técnico sin tareas | El responsable debe ser su `sub` real; IDs de ejemplo u otro realm no coinciden. |
| Chatbot no disponible | `ollama list`, modelo, interfaz de escucha, firewall y acceso desde Docker. Loopback en el contenedor apunta al propio contenedor. |
| Monitoreo vacío | Inventario instalado, simulador accesible y workers habilitados; esperar muestras para el histórico. |
| Puerto/binarios ocupados | Cerrar instancias anteriores o compilar a salida separada; revisar `docker compose ps`. |
| Pruebas verdes con omisiones | Comprobar variables, servicios y modos de inspección; omitir no equivale a aprobar. |
| Sonar publica y termina con error | Revisar el panel: publicar análisis y aprobar Gate son resultados distintos. |
