# Implementación: alta disponibilidad durante actualización

## Cambios realizados

- `SolarUY.Presentation/Dockerfile`: imagen de publicación y runtime .NET 8 con endpoint de salud comprobado por Docker.
- `.dockerignore`: excluye secretos locales, Git y artefactos de compilación del contexto de imagen.
- `compose.yaml`: agrega las instancias `solaruy-a` y `solaruy-b`, Nginx y una red compartida; ambas montan `dataprotection_keys`. Los secretos `SOLARUY_WEB_CLIENT_SECRET` y `SOLARUY_DIRECTORY_CLIENT_SECRET` se configuran por separado en `.env` y llegan a ambas instancias. La recolección histórica queda habilitada solo en A.
- `compose.yaml`: la autoridad OIDC y el Admin REST de Keycloak usan la dirección interna `keycloak:8080`; Keycloak conserva `http://localhost:8080` para el navegador y resuelve dinámicamente las URLs de backchannel. El simulador del inversor en el host se alcanza por `host.docker.internal`.
- `SolarUY.Infrastructure/DependencyInjection.cs`: el cliente TCP del inversor acepta tanto una dirección IP como un hostname, para permitir la dirección `host.docker.internal` en contenedores. El servidor TCP del simulador conserva la validación de dirección IP.
- `SolarUY.Infrastructure/Identity/KeycloakTecnicoDirectory.cs`: permite HTTP a `keycloak:8080` solo en entorno Development, para la llamada interna al contenedor; las direcciones externas siguen requiriendo HTTPS.
- `compose.yaml`: el servicio auxiliar `keycloak-data-permissions` asigna el volumen H2 al UID 1000 de la imagen Keycloak antes de iniciar Keycloak.
- `deploy/nginx/monitoreo.conf.template`: usa `ip_hash` para repartir solicitudes entre A y B, reintenta en el backend disponible y admite WebSockets.
- `deploy/nginx/monitoreo.conf.template`: aumenta los buffers de cabecera del proxy para aceptar la respuesta `Set-Cookie` del inicio de sesión OIDC y las cookies entrantes de autenticación.
- `SolarUY.Presentation/Program.cs`: agrega `GET /health` como endpoint anónimo. Se conserva el encabezado de instancia existente para observar qué proceso atendió la solicitud.
- `.env.example`, `README.md` y esta carpeta: documentan la configuración y operación.

## Preparación local

1. Si falta `.env`, copiar `.env.example`. Cada integrante prepara su copia local. Completar contraseñas de MariaDB, del administrador de Keycloak y de los usuarios de prueba; se pueden elegir para ese entorno. Dejar inicialmente vacíos los dos secretos de cliente.
2. Iniciar solo la base y Keycloak para que se importe el realm y se generen los clientes:

   ```powershell
   docker compose up -d mariadb keycloak
   docker compose ps
   ```

   Esperar a que Keycloak termine de arrancar y abrir `http://localhost:8080`.
3. Obtener y agregar por separado los secretos de **Clients → solaruy-web → Credentials** y **Clients → solaruy-tecnico-directory → Credentials**:

   ```text
   SOLARUY_WEB_CLIENT_SECRET=valor_del_secreto
   SOLARUY_DIRECTORY_CLIENT_SECRET=valor_del_secreto_del_directorio
   ```

   El primero permite el login de usuarios; el segundo permite que el servidor consulte usuarios con rol `Tecnico` habilitados al listar o asignar responsables de mantenimiento. Son dos clientes y secretos distintos. Compose entrega ambos a A y B. Los `user-secrets` se siguen usando al iniciar Presentation directamente en el host; los contenedores no leen esos secretos del host.
4. Si el realm ya existía, agregar en **Clients → solaruy-web → Settings** las URLs de retorno indicadas abajo; el import inicial no modifica realms existentes.
5. Levantar la pila desde la raíz:

   ```powershell
   docker compose up -d --build
   docker compose ps
   Invoke-WebRequest http://localhost:18088/health
   ```

6. Si la base está vacía, aplicar las migraciones una sola vez desde el host, cuando MariaDB aparezca `healthy` en `docker compose ps`:

   ```powershell
   dotnet ef database update --project SolarUY.Infrastructure/SolarUY.Infrastructure.csproj --startup-project SolarUY.Presentation/SolarUY.Presentation.csproj -- --environment Development
   ```

7. El cliente `solaruy-web` de Keycloak debe permitir los callbacks de `localhost` y `127.0.0.1` en el puerto 18088. Por cada host, registrar `/signin-oidc` como redirect URI, el origen como web origin y `/*` como post-logout redirect. Estas direcciones se añadieron a `keycloak/realm/solaruy-realm.json` para realms nuevos.

## Demostrar caída de una instancia

En una terminal, consultar repetidamente el proxy:

```powershell
while ($true) {
  try {
    $r = Invoke-WebRequest -UseBasicParsing http://localhost:18088/health
    "$($r.StatusCode) $($r.Headers['X-SolarUY-Instancia'])"
  }
  catch { "ERROR: $($_.Exception.Message)" }
  Start-Sleep -Milliseconds 500
}
```

Mientras esa consulta corre, en otra terminal detener A:

```powershell
docker compose stop solaruy-a
```

La consulta debe seguir obteniendo respuestas y mostrar la otra instancia. Detén con Ctrl+C la consulta e inicia de nuevo la instancia detenida. Por ejemplo, si detuviste A:

```powershell
docker compose start solaruy-a
docker compose ps
```

## Demostrar actualización secuencial

Mantener la consulta repetida anterior en ejecución. En otra terminal, construir una etiqueta nueva y reemplazar A; esperar a que esté `healthy` antes de reemplazar B:

```powershell
$env:SOLARUY_VERSION = 'entrega3-v2'
docker compose build solaruy-a
docker compose up -d --no-deps solaruy-a
docker compose ps solaruy-a
docker compose up -d --no-deps --force-recreate solaruy-b
docker compose ps solaruy-b
```

La dirección pública debe seguir respondiendo durante los dos reemplazos. El encabezado permite confirmar cuál instancia respondió. La afinidad mantiene las conexiones en la instancia seleccionada mientras esté disponible; si esa instancia se detiene, el circuito Blazor Server debe reconectarse y reconstruirse en la sobreviviente. Data Protection permite validar la cookie entre instancias, pero no copia el estado en memoria del circuito.

## Verificación ejecutada y pendiente

- `docker compose config --quiet`: aprobado con valores temporales de validación; no se guardaron en archivos. Los secretos de cliente se permiten vacíos durante el arranque inicial de Keycloak; se deben completar antes de probar login y el directorio de técnicos.
- El primer `docker compose up -d --build` intentó descargar `solaruy:local` para B, y la construcción de A falló porque el grupo `app` ya existe en la imagen base oficial ASP.NET. Se configuró B para usar la imagen local y se quitó la creación redundante del usuario/grupo del Dockerfile. La construcción posterior al ajuste aún debe ejecutarse.
- El primer acceso web devolvió HTTP 500 porque la app intentaba leer el documento OIDC en `host.docker.internal:8080`, inaccesible desde los contenedores con Keycloak publicado solo en loopback. Se comprobó que `keycloak:8080` responde desde A y se actualizó la configuración para usar endpoints internos con hostname frontal/backchannel separado. Al recrear Keycloak se debe volver a obtener sus secretos de cliente.
- El arranque siguiente reveló dos fallos adicionales: A rechazaba `host.docker.internal` porque Infrastructure solo aceptaba IPs, y Keycloak no podía escribir en el volumen H2 nuevo. Se amplió la validación del cliente TCP para aceptar hostnames y se agregó un inicializador de permisos para el volumen Keycloak.
- El primer retorno de login devolvió 502 porque Nginx registró `upstream sent too big header` en `POST /signin-oidc`; se aumentó el tamaño de buffers de respuesta del proxy. Pendiente recrear Nginx y repetir el login.
- El navegador informó `Request Header Or Cookie Too Large`; también se aumentaron los buffers de cabeceras entrantes. Pendiente recrear Nginx y verificar login desde una sesión limpia del navegador.
- Mantenimiento mostró la validación de URL de Keycloak aunque el secreto estaba presente en A y B: la regla HTTPS bloqueaba `http://keycloak:8080` en Development. Se limitó la excepción a ese hostname interno y entorno; `dotnet build SolarUY.Infrastructure/SolarUY.Infrastructure.csproj --no-restore` compiló correctamente, con advertencias de NuGet por falta de conexión al índice de vulnerabilidades.
- `git diff --check`: sin errores de whitespace.
- No se ejecutaron pruebas automatizadas. La construcción Docker aún debe repetirse después de la corrección.
- El usuario confirmó que ejecutó las demostraciones de caída de una instancia y actualización secuencial, comprobando la disponibilidad pública.

## Arranque reproducible corregido

Compose incluye un simulador TCP en la red privada; A y B se conectan al mismo servicio y conservan la exclusión mutua de adquisición. Ollama del host se resuelve mediante host.docker.internal con permiso explícito limitado a ese nombre. Se configura la dirección de Nginx como proxy de confianza y MariaDB con buffer pool de 512 MiB.

Se añadió `Iniciar-Demo.ps1 -AltaDisponibilidad`: valida la configuración efectiva de Compose sin imprimir secretos, inicia dependencias, espera MariaDB, aplica migraciones y construye/levanta el conjunto. El modo no restablece datos. Se verificaron sintaxis PowerShell, `docker compose config --quiet` y construcción de las imágenes web/simulador. No se ejecutó este arranque contra la base habitual ni se repitió el login real o el relevo en navegador durante esta corrección; se conserva la demostración anterior informada por el usuario.
