# Especificaciones técnicas: alta disponibilidad durante actualización

## Contexto y alcance

SolarUY es una aplicación Blazor Server en .NET 8. Se ejecutará en dos procesos de Presentation detrás de Nginx, junto con MariaDB y Keycloak definidos en el Compose existente. La entrega demuestra que el endpoint web sigue respondiendo cuando una instancia se detiene o se reemplaza.

La afinidad mantiene las solicitudes y la conexión WebSocket en una instancia disponible. Si el proceso que guarda un circuito Blazor Server cae, el estado en memoria no se transfiere; el navegador puede reconectar y reconstruir la vista en la instancia sobreviviente. Data Protection comparte las claves de cookies y datos protegidos, pero no comparte el circuito en memoria.

## Domain

No se modifica el dominio. La alta disponibilidad es una propiedad del despliegue y no cambia las reglas de negocio ni las entidades.

## Application

No se modifican casos de uso ni contratos de Application. Las dos instancias consultan la misma base de datos mediante la configuración existente.

El worker de recolección histórica se habilita solo en `solaruy-a`: el worker actual no tiene elección distribuida de propietario y habilitarlo en ambas instancias podría duplicar la recolección.

## Infrastructure

### Contenedores

- `SolarUY.Presentation/Dockerfile` publica la aplicación .NET 8 en una imagen de ejecución ASP.NET 8.
- El contexto de construcción excluye secretos locales, resultados de compilación y metadatos de Git mediante `.dockerignore`.
- El Compose existente se amplía con los servicios `solaruy-a`, `solaruy-b` y `nginx`, conservando MariaDB, Keycloak y phpMyAdmin.
- Las dos instancias usan la misma imagen etiquetada y configuración externa. La cadena MariaDB usa el nombre de servicio `mariadb`; no se aplican migraciones automáticamente al iniciar.
- Las instancias no publican puertos de host; reciben tráfico web solo desde Nginx en la red del Compose.

### Balanceo y continuidad

- Nginx distribuye solicitudes entre A y B con `ip_hash`, reintenta en el otro backend si uno falla y configura HTTP/1.1, `Upgrade`/`Connection` y tiempos de espera para WebSockets de SignalR y Blazor Server.
- La imagen de cada instancia expone `/health`, una respuesta anónima de disponibilidad de Presentation. Docker usa ese endpoint para determinar la salud del contenedor.
- MariaDB conserva su comprobación de salud existente. La disponibilidad de Keycloak no forma parte del endpoint `/health` de SolarUY.
- El volumen nombrado `dataprotection_keys` se monta en ambas instancias. Presentation persiste ahí las claves y fija el nombre de aplicación Data Protection `SolarUY` para compartirlas.
- `Authentication:OpenIdConnect:ClientSecret` se inyecta en ambas instancias desde `SOLARUY_WEB_CLIENT_SECRET` en el `.env` local de Compose. El secreto es el mismo para el cliente confidencial Keycloak `solaruy-web`.
- `Keycloak:Directory:ClientSecret` se inyecta en ambas instancias por separado desde `SOLARUY_DIRECTORY_CLIENT_SECRET`. Ese secreto permite que la aplicación use las credenciales de servicio del cliente `solaruy-tecnico-directory` para consultar técnicos habilitados. Los `user-secrets` del host siguen utilizándose cuando Presentation se ejecuta directamente con `dotnet run`; no se montan dentro de los contenedores.

## Presentation

- Se agrega `GET /health` permitido anónimamente para el health check. No expone información de usuario ni de configuración.
- El middleware existente añade `X-SolarUY-Instancia` a las respuestas para identificar qué instancia atendió la solicitud durante la demostración.
- Se conservan autenticación OIDC, autorización y SignalR existentes. Nginx no almacena sesiones de usuario.

## Operación y demostración

- `docker compose up -d --build` construye y levanta la pila local; SolarUY queda disponible por Nginx en el puerto 18088.
- La caída se demuestra deteniendo una instancia y consultando `/health` a través de Nginx, que debe seguir respondiendo desde la instancia sobreviviente.
- La actualización se demuestra construyendo y recreando un servicio a la vez, esperando que vuelva a estar saludable antes de reemplazar el otro. La solicitud pública debe seguir respondiendo durante ambos pasos.
- Las claves de Data Protection y la base de datos persisten en volúmenes de Docker durante la recreación de las instancias.

## Verificación

- Validar el Compose con `docker compose config --quiet` después de completar las variables requeridas del `.env`.
- Verificar manualmente salud, identificador de instancia y continuidad al detener y reemplazar una instancia por vez.
- No se requiere modificar ni ejecutar pruebas automatizadas para esta especificación.
