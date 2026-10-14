# Keycloak local para SolarUY

El contenedor importa `realm/solaruy-realm.json` al iniciar. El realm crea el cliente confidencial `solaruy-web`, los roles `Administrador`, `Tecnico` y `Consulta`, y un usuario de prueba para cada rol. El mapper del cliente incorpora los roles del realm al ID token como `realm_access.roles`.

## Inicio

1. Copiar `keycloak/.env.example` a `keycloak/.env` y reemplazar todas las contraseÃ±as de ejemplo por valores locales.
2. Desde la raíz del repositorio, ejecutar `docker compose --env-file .env -f compose.yaml up -d`.
3. En `SolarUY.Presentation`, guardar el secreto generado para el cliente confidencial usando `dotnet user-secrets set "Authentication:OpenIdConnect:ClientSecret" "<client-secret>"`. El secreto del cliente se obtiene desde la consola de administraciÃ³n de Keycloak en `http://localhost:8080`.
4. Ejecutar SolarUY con el perfil `http` (puerto 5219) o `https` (puerto 7271).

Usuarios de prueba: `admin.test`, `tecnico.test` y `consulta.test`. Sus contraseÃ±as son las variables respectivas de `.env`.

La autoridad (`http://localhost:8080/realms/solaruy`) y el cliente (`solaruy-web`) estÃ¡n en `SolarUY.Presentation/appsettings.json`. Para producciÃ³n, configurar esos valores con variables `Authentication__OpenIdConnect__Authority` y `Authentication__OpenIdConnect__ClientId`; configurar el secreto con `Authentication__OpenIdConnect__ClientSecret`. No utilizar `start-dev` fuera del entorno local.
