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

Cada integrante conserva sus secretos fuera de Git. El import omite realms existentes: cambiar `.env` no actualiza usuarios/clientes ya creados. Iniciar-Demo comparte la conexión y los secretos de Keycloak de `.env` entre Docker y la web local. Con dotnet run directo se usan user-secrets o variables de entorno.

## Chatbot local

Instala Ollama y ejecuta `ollama pull qwen3:8b`. Para que Docker lo alcance, detén la instancia previa y abre una terminal dedicada:

```powershell
$env:OLLAMA_HOST = "0.0.0.0:11434"
ollama serve
```

Restringe el acceso mediante firewall al entorno de desarrollo. Compose utiliza `host.docker.internal:11434`; la web local puede usar `127.0.0.1:11434`. El resto de la aplicación puede utilizarse sin respuestas del modelo.

## Ollama en otra PC

En la PC del modelo, instala Ollama y descarga `ollama pull qwen3:8b`. Cierra la aplicación Ollama junto al reloj si ya está iniciada y abre una terminal dedicada:

```powershell
$env:OLLAMA_HOST = "0.0.0.0:11434"
ollama serve
```

Permite TCP 11434 en el firewall solo desde la PC de SolarUY o su red Docker/VPN. En SolarUY usa la IP de esa PC (ejemplo `192.168.1.50`):

```powershell
# Web local
.\Iniciar-Demo.ps1 -OllamaRemoto "http://192.168.1.50:11434/"
# Dos webs en Docker
.\Iniciar-Demo.ps1 -AltaDisponibilidad -OllamaRemoto "http://192.168.1.50:11434/"
```

La opción se aplica a ese arranque y, en Docker, configura ambas webs. Para conservarla en futuros arranques Docker, añade a `.env`:

```dotenv
SOLARUY_OLLAMA_ENDPOINT=http://192.168.1.50:11434/
SOLARUY_OLLAMA_PERMITIR_REMOTO=true
```

En desarrollo local sin el parámetro, configura `Chatbot:Ollama:Endpoint` y `Chatbot:Ollama:PermitirRemoto=true` mediante user-secrets o variables de entorno. `.env` solo configura Docker. La URL debe ser la base `/`, sin `/api/chat`, credenciales, consulta ni fragmento; se admiten HTTP y HTTPS con certificado válido.

Para PCs en redes diferentes, conecta ambas mediante VPN y usa la IP de VPN, accesible también desde los contenedores. No publiques Ollama directamente en Internet: esta integración no añade autenticación al proveedor. Desde la PC de SolarUY puedes comprobar `Invoke-RestMethod "http://192.168.1.50:11434/api/tags"`; debe listar `qwen3:8b`. El navegador accede a SolarUY; quien conecta al modelo es su servidor. Más detalles en la [configuración oficial de Ollama](https://docs.ollama.com/faq#how-can-i-expose-ollama-on-my-network).

## Ejecutar con Docker

```powershell
.\Iniciar-Demo.ps1 -AltaDisponibilidad
```

El script aplica migraciones y levanta dos webs, Nginx y el simulador, conservando datos. Aplicación: **http://localhost:18088**; Keycloak: **8080**; phpMyAdmin: **8081**; MariaDB desde el host: **3307**.

Para revisar estado: `docker compose ps`; para registros: `docker compose logs --tail 100 solaruy-a solaruy-b simulador nginx`. Detén con `docker compose stop`. No uses `down -v` para un cierre normal: elimina los volúmenes.

## Desarrollo con la web local

Iniciar-Demo toma de Compose/.env la conexión MariaDB y ambos secretos de Keycloak, igual que Docker. Los aplica como variables de entorno temporales y restaura el entorno al terminar: **no necesitas ejecutar `dotnet user-secrets set` al usar el script**. Solo debes completar `.env` con los secretos copiados de Keycloak. Keycloak se usa en localhost:8080 y MariaDB en 127.0.0.1:3307.

Después ejecuta únicamente `.\Iniciar-Demo.ps1`. El script detiene las webs/Nginx/simulador de Docker, inicia MariaDB, Keycloak y phpMyAdmin, espera su disponibilidad y aplica migraciones usando la configuración local. Compila e inicia el simulador como proceso local y después la web.

Web: **http://localhost:5219**; simulador TCP: **127.0.0.1:5055**. No inicies el simulador en otra terminal. Ctrl+C detiene la web y el simulador iniciado por el script; los contenedores de dependencias permanecen activos y conservan datos. Sus logs están en `artifacts/demo/`. Si 5219 o 5055 están ocupados, cierra el proceso local anterior. Ollama se inicia aparte, local o remoto según la sección anterior.

## Parámetros de `Iniciar-Demo.ps1`

Estos parámetros se usan en modo local; no se combinan con `-AltaDisponibilidad`:

- `-SinGuardarHistorial`: mantiene el monitoreo en vivo, pero no guarda lecturas históricas durante ese arranque.
- `-ReiniciarDatos`: carga `solaruy_pruebas.sql` en la base MariaDB del Compose después de migrarla. Reemplaza inventario, mantenimiento, lecturas, salud y auditoría de demostración en una transacción; conserva tablas e historial de migraciones. Úsalo solo con datos locales que puedas perder.
- `-TecnicoId <GUID>`: asigna los datos de demostración a ese técnico. Solo se permite junto con `-ReiniciarDatos`.

Ejemplos:

```powershell
.\Iniciar-Demo.ps1 -SinGuardarHistorial
.\Iniciar-Demo.ps1 -ReiniciarDatos
.\Iniciar-Demo.ps1 -ReiniciarDatos -TecnicoId "00000000-0000-0000-0000-000000000001"
```

El arranque habitual no necesita parámetros: `.\Iniciar-Demo.ps1`.

## Datos y problemas habituales

En una base vacía, Administrador crea parque, nodos/inversores y paneles. Las tareas se asignan a técnicos reales del realm. `-ReiniciarDatos` es opcional y reemplaza datos de demostración; no se usa en el arranque habitual.

Si falla login, revisa secreto y redirecciones de Keycloak. Si falla el chat, revisa `ollama list`, dirección y firewall. Si faltan mediciones, revisa inventario, simulador y logs; el histórico necesita esperar muestras. Los procedimientos específicos están en [Keycloak](../keycloak/TECNICOS.md) y [Nginx](../deploy/nginx/README.md).
