# Implementación de req002: autenticación con Keycloak

## Cambios y tareas completadas

- Se usó la configuración local existente en `.env` y se inició el servicio Keycloak con Docker Compose. Keycloak 26.8.1 importó correctamente el realm `solaruy`.
- Se obtuvo el secreto del cliente confidencial `solaruy-web` y se guardó con `dotnet user-secrets` para `SolarUY.Presentation`. El secreto no se incluyó en archivos versionados ni en este informe.
- Se recorrió el flujo OpenID Connect en desarrollo con los tres usuarios de prueba. El ID token incluyó el rol esperado para cada uno y la aplicación mapeó ese rol a las claims de .NET.
- `admin.test` accedió a la página protegida de alta de parques. `tecnico.test` y `consulta.test` fueron redirigidos a `/Account/AccessDenied`.
- Se verificó que `Authority`, `ClientId` y `ClientSecret` se leen de la configuración del host, que admite variables de entorno.

## Archivos modificados

- `prompting/req002_login_keycloak/tasks.md`: se actualizaron las tareas completadas y se registró el pendiente de las URLs de producción.
- `prompting/req002_login_keycloak/implementations.md`: se agregó este informe.

También se guardó el secreto de `solaruy-web` en el almacén local de .NET User Secrets. No se modificaron archivos de código ni se alteró el `.env` existente.

## Verificación

- Autenticación OIDC local y autorización de la ruta administrativa verificadas con los tres usuarios.
- No se ejecutó `dotnet test`.
- La tarea 5.3 queda pendiente porque la URL HTTPS pública de SolarUY aún no está definida; no se registraron redirecciones de producción ficticias en Keycloak.
