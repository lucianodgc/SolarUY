# Keycloak local para SolarUY

El contenedor importa `realm/solaruy-realm.json` al iniciar. El realm crea el cliente confidencial `solaruy-web`, los roles `Administrador`, `Tecnico` y `Consulta`, y un usuario de prueba para cada rol. El mapper del cliente incorpora los roles del realm al ID token como `realm_access.roles`.

## Inicio

1. Copiar `.env.example` a `.env` y reemplazar todas las contraseñas de ejemplo por valores locales.
2. Desde la raíz del repositorio, ejecutar `docker compose --env-file .env -f compose.yaml up -d`.
3. En `SolarUY.Presentation`, guardar el secreto generado para el cliente confidencial usando `dotnet user-secrets set "Authentication:OpenIdConnect:ClientSecret" "<client-secret>"`. El secreto del cliente se obtiene desde la consola de administración de Keycloak en `http://localhost:8080`.
4. Ejecutar SolarUY con el perfil `http` (puerto 5219) o `https` (puerto 7271).

Usuarios de prueba: `admin.test`, `tecnico.test` y `consulta.test`. Sus contraseñas son las variables respectivas de `.env`.

La autoridad (`http://localhost:8080/realms/solaruy`) y el cliente (`solaruy-web`) están en `SolarUY.Presentation/appsettings.json`. Para producción, configurar esos valores con variables `Authentication__OpenIdConnect__Authority` y `Authentication__OpenIdConnect__ClientId`; configurar el secreto con `Authentication__OpenIdConnect__ClientSecret`. No utilizar `start-dev` fuera del entorno local.

## Verificación operativa pendiente (H-09)

La preparación del equipo y las redirecciones de producción siguen pendientes hasta que se realicen las acciones correspondientes. La existencia de estos pasos no acredita su ejecución.

- El equipo debe acordar cómo distribuir las credenciales locales fuera de Git. Cada integrante prepara su `.env` desde `.env.example`; .NET recibe la conexión y los secretos por user-secrets o variables de entorno. El `.env` de Compose no configura automáticamente .NET.
- Cada integrante debe comprobar `docker compose up -d`, `docker compose ps`, aplicar las migraciones con el comando del README raíz y probar login, consulta y una operación autorizada en sus datos de prueba. Registrar integrante, fecha y resultado en el mecanismo de seguimiento del equipo, sin publicar secretos.
- El responsable del despliegue debe definir la URL HTTPS pública y la autoridad de Keycloak. Solo entonces corresponde registrar las URLs reales de callback `/signin-oidc` y logout `/signout-callback-oidc` del cliente, y verificar login/logout en ese entorno. No se agregan URLs supuestas al realm compartido.

Las tareas REQ003 1.3 y REQ002 5.3 permanecen abiertas; una ejecución aislada de auditoría no las completa. Para el directorio y asignaciones de mantenimiento, ver [TECNICOS.md](TECNICOS.md).
